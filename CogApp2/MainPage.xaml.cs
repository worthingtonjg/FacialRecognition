using CogApp2.Model;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace CogApp2
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private MediaCapture _mediaCapture;
        private FaceDetectionEffect _faceDetectionEffect;
        private IFaceClient _faceClient;
        private string _subscriptionKey = "<Insert Azure Face Api Key>";
        private string _apiRoot = "https://westus.api.cognitive.microsoft.com/face/v1.0";
        private string _personGroupId = "myfriends";
        private bool _addPerson;

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _faceClient = new FaceClient(new ApiKeyServiceClientCredentials(_subscriptionKey), new DelegatingHandler[] { })
            {
                BaseUri = new Uri(_apiRoot)
            };
            
            _mediaCapture = new MediaCapture();
            await _mediaCapture.InitializeAsync();

            PreviewElement.Source = _mediaCapture;
            await _mediaCapture.StartPreviewAsync();

            await InitFaceGroup();

            await CreateFaceDetectionEffectAsync();
        }

        private async Task InitFaceGroup()
        {
            try
            {
                PersonGroup group = await _faceClient.PersonGroup.GetAsync(_personGroupId);
            }
            catch (APIErrorException ex)
            {
                if (ex.Body.Error.Code == "PersonGroupNotFound")
                {
                    await _faceClient.PersonGroup.CreateAsync(_personGroupId, _personGroupId);
                    await _faceClient.PersonGroup.TrainAsync(_personGroupId);
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task CreateFaceDetectionEffectAsync()
        {
            // Create the definition, which will contain some initialization settings
            var definition = new FaceDetectionEffectDefinition();

            // To ensure preview smoothness, do not delay incoming samples
            definition.SynchronousDetectionEnabled = false;

            // In this scenario, balance speed and accuracy
            definition.DetectionMode = FaceDetectionMode.Balanced;

            // Add the effect to the preview stream
            _faceDetectionEffect = (FaceDetectionEffect)await _mediaCapture.AddVideoEffectAsync(definition, MediaStreamType.VideoPreview);

            // Register for face detection events
            _faceDetectionEffect.FaceDetected += FaceDetectionEffect_FaceDetected;

            // Choose the shortest interval between detection events
            _faceDetectionEffect.DesiredDetectionInterval = TimeSpan.FromMilliseconds(1000);

            // Start detecting faces
            _faceDetectionEffect.Enabled = true;
        }

        private async void FaceDetectionEffect_FaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
            Debug.WriteLine($"{args.ResultFrame.DetectedFaces.Count} faces detected");

            if (args.ResultFrame.DetectedFaces.Count == 0) return;

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    _faceDetectionEffect.FaceDetected -= FaceDetectionEffect_FaceDetected;

                    // Do stuff here
                    var bitmap = await GetWriteableBitmapFromPreviewFrame();
                    var file = await SaveBitmapToStorage(bitmap);
                    await AddPerson(file);
                    var faces = await FindFaces(file);
                    var identities = await Identify(faces);
                    var candidates = await ExtractTopCandidate(identities, faces);

                    string json = JsonConvert.SerializeObject(candidates, Formatting.Indented);

                    ResultText.Text = json;

                }
                finally
                {
                    _faceDetectionEffect.FaceDetected += FaceDetectionEffect_FaceDetected;
                }
            });
        }

        private async Task<WriteableBitmap> GetWriteableBitmapFromPreviewFrame()
        {
            var previewProperties = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;

            var videoFrame = new VideoFrame(BitmapPixelFormat.Bgra8, (int)previewProperties.Width, (int)previewProperties.Height);

            var frame = await _mediaCapture.GetPreviewFrameAsync(videoFrame);

            SoftwareBitmap frameBitmap = frame.SoftwareBitmap;

            WriteableBitmap bitmap = new WriteableBitmap(frameBitmap.PixelWidth, frameBitmap.PixelHeight);

            frameBitmap.CopyToBuffer(bitmap.PixelBuffer);

            // Close the frame
            frame.Dispose();
            frame = null;

            return bitmap;
        }

        private async Task<StorageFile> SaveBitmapToStorage(WriteableBitmap bitmap)
        {
            var myPictures = await StorageLibrary.GetLibraryAsync(Windows.Storage.KnownLibraryId.Pictures);
            StorageFile file = await myPictures.SaveFolder.CreateFileAsync("_photo.jpg", CreationCollisionOption.ReplaceExisting);

            using (var captureStream = new InMemoryRandomAccessStream())
            {
                using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await bitmap.ToStreamAsJpeg(captureStream);

                    var decoder = await BitmapDecoder.CreateAsync(captureStream);
                    var encoder = await BitmapEncoder.CreateForTranscodingAsync(fileStream, decoder);

                    var properties = new BitmapPropertySet {
                            { "System.Photo.Orientation", new BitmapTypedValue(PhotoOrientation.Normal, PropertyType.UInt16) }
                        };

                    await encoder.BitmapProperties.SetPropertiesAsync(properties);

                    await encoder.FlushAsync();
                }
            }

            return file;
        }

        public async Task<IList<DetectedFace>> FindFaces(StorageFile file)
        {
            IList<DetectedFace> result = new List<DetectedFace>();

            using (var stream = await file.OpenStreamForReadAsync())
            {
                result = await _faceClient.Face.DetectWithStreamAsync(stream, true, false, new FaceAttributeType[]
                {
                    FaceAttributeType.Gender,
                    FaceAttributeType.Age,
                    FaceAttributeType.Smile,
                    FaceAttributeType.Emotion,
                    FaceAttributeType.Glasses,
                    FaceAttributeType.Hair,
                               });
            }

            return result;
        }

        public async Task<IList<IdentifyResult>> Identify(IList<DetectedFace> faces)
        {
            if (faces.Count == 0) return new List<IdentifyResult>();

            IList<IdentifyResult> result = new List<IdentifyResult>();

            try
            {
                TrainingStatus status = await _faceClient.PersonGroup.GetTrainingStatusAsync(_personGroupId);

                if (status.Status != TrainingStatusType.Failed)
                {
                    IList<Guid> faceIds = faces.Select(face => face.FaceId.GetValueOrDefault()).ToList();

                    result = await _faceClient.Face.IdentifyAsync(_personGroupId, faceIds, null);
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return result;
        }

        public async Task<List<Identification>> ExtractTopCandidate(IList<IdentifyResult> identities, IList<DetectedFace> faces)
        {
            var result = new List<Identification>();

            foreach (var face in faces)
            {
                var identifyResult = identities.Where(i => i.FaceId == face.FaceId).FirstOrDefault();

                var identification = new Identification
                {
                    Person = new Person { Name = "Unknown" },
                    Confidence = 1,
                    Face = face,
                    IdentifyResult = identifyResult
                };

                result.Add(identification);

                if(identifyResult != null && identifyResult.Candidates.Count > 0)
                {
                
                    // Get top 1 among all candidates returned
                    IdentifyCandidate candidate = identifyResult.Candidates[0];
                   
                    var person = await _faceClient.PersonGroupPerson.GetAsync(_personGroupId, candidate.PersonId);

                    identification.Person = person;
                    identification.Confidence = candidate.Confidence;
                }
            }

            return result;
        }

        private void AddPersonButton_Click(object sender, RoutedEventArgs e)
        {
            if(PersonName.Text != null)
            {
                _addPerson = true;
            }
        }

        private async Task AddPerson(StorageFile file)
        {
            if (!_addPerson) return;

            try
            {
                using (var s = await file.OpenStreamForReadAsync())
                {
                    Person newPerson = await _faceClient.PersonGroupPerson.CreateAsync(_personGroupId, PersonName.Text);
                    await _faceClient.PersonGroupPerson.AddPersonFaceFromStreamAsync(_personGroupId, newPerson.PersonId, s);
                    await _faceClient.PersonGroup.TrainAsync(_personGroupId);
                }

            }
            finally
            {
                PersonName.Text = "";
                _addPerson = false;
            }
        }
    }
}
