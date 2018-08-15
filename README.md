# Facial Recognition

This code demonstrates how to use the Microsoft Cognitive Service Face API.  

## How to Run the Code

To run the code you will need:

  - Visual Studio 2017
  - Windows 10 November Update (Build 10586)
  - An Azure subscription 
  - A Face API Cognitive Service setup and running in Azure
  
1. Clone the repo
2. Open CogApp2.sln
3. Edit MainPage.xaml.cs
4.  Change the following code, to have values for your cognitive service:

```C#
        private string _subscriptionKey = "";
        private string _apiRoot = "https://westus.api.cognitive.microsoft.com/face/v1.0";
        private string _personGroupId = "myfriends";
```

**_subscriptionKey**

In your azure portal, open your Face Cognitive Service.  Under Resource Management => Keys, copy one of your keys.

**_apiRoot**

You can actually leave the end point as is: https://westus.api.cognitive.microsoft.com/face/v1.0

**_personGroupId**

Faces are categorized and stored in person groups.  If you already have a person group, then put the id for your group here.  Otherwise set this to whatever makes sense to you.  You can leave it as "myfriends" if you want to.

**Run the app**

Now run the app on the "Local Machine".  It should run, and display a video preview.  Once things initialize, each second the video preview is analyzed and sent to the cognitive services.  The results are displayed in the text box.

If this is your first time to run it, it won't have any faces defined in the face group.  Put a name in the textbox at the bottom of the interface, and press  the "Add Person" button.  This will analyze the current frame, detect the face,  create a new person in your person group for that face, and re-train the person group to match that face to the name you entered in the textbox.

## Tutorial

Below is a step by step tutorial / explanation of the code, and how it works.  Topics discussed include:

- **CaptureElement** - allows you to preview a feed from a camera.
- **MediaCapture** - allows you to connect to a web cam camera and take video / images.
- **Local Face Detection** - Detect faces locally, so we don't need to call the cognitive service as much.
- **Capture Frames** - How to capture frames from the video preview 
- **Save Frames to Pictures Folder** - How to save captured frames to the Pictures Folder
- **Find Faces with Cognitive Service** - Analyze images for faces, identifying facial attributes
- **Identify Faces with Cognitive Services** - Analyzes faces found and matche them to people in your person group
- **Train Person Group with new Faces** - Add new faces / identities to your person group, and retrain.

### Step 1: Create new UWP Application
- Open Visual Studio 2017, and choose File => New Project
- In the New Project Dialog choose, under Visual C#, Windows Universal, Choose "Blank App (Universal Windows)"
- You will be prompted with a dialog to choose a target and minimum platform.  Below are the settings I chose, but these may be different depending on what is on your machine.  The minimum version cannot be any older than Build 10586.
  - Target Version: Windows 10 Fall Creators Update (10.0; Build 16299)
  - Minimum Version: Windows 10 November Update (10.0; Build 10586)

### Step 2: Add Capabilities
- Edit the Package.appxmanifest and add the following capabilities:

1. Microphone
2. Pictures Library
3. WebCam

To use the web camera, we also have to enable the microphone.

### Step 3: Setup the Video Preview
- Open *MainPage.xaml* and paste in the following code snippet, in place of the empty <Grid></Grid>

```xaml
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition></ColumnDefinition>
            <ColumnDefinition></ColumnDefinition>
        </Grid.ColumnDefinitions>
        
        <CaptureElement Grid.Column="0" x:Name="PreviewElement"></CaptureElement>
        <TextBox Grid.Column="1" x:Name="ResultText" TextWrapping="Wrap"></TextBox>
    </Grid>
```
- Open *MainPage.xaml.cs*, and add an override for *OnNavigatedTo*, and make it async.  We need setup some async code when the app launches, and we can't do async in the constructor.

```c#
	protected async override void OnNavigatedTo(NavigationEventArgs e)
	{
	}
```
- At the top, add the following using statement:

```c#
using Windows.Media.Capture;
```

- Then add the following private variable at the top of the MainPage class:

```c#
private MediaCapture _mediaCapture;
```

- In the OnNavigatedTo method add the following code:

```c#
		_mediaCapture = new MediaCapture();
		await _mediaCapture.InitializeAsync();

		PreviewElement.Source = _mediaCapture;
		await _mediaCapture.StartPreviewAsync();
```

The *MediaCapture* class allows us to connect to the attached Web Camera.  

The *PreviewElement.Source* is set to point at _mediaCapture, allowing us to preview the video on our screen.

**Run the application**  

You should get prompted for permission to use the microphone and web camera.  If you say yes, to both you should see a video preview from the camera on your screen.

> **Note:** On my Surface Book 2 the *MediaCapture* class defaults to the front facing camera.  It is possible you have a different camera setup, so you may need to look into the documentation for more information about setting which camera to use.
>
>[View UWP Camera Documentation](https://docs.microsoft.com/en-us/windows/uwp/audio-video-camera/camera)

### Step 4: Add local face detection

We want to be able to detect faces locally.  This will allow us to call the cognitive services less frequently (only when faces are in the frame).  To do this we can use the built in face detection capability of Windows 10 by adding a Face Detection Effect.  

- Add a two new using statements:

```c#
using Windows.Media.Core;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.UI.Core;
```

- Add a new variable at the top of the code:

```c#
private FaceDetectionEffect _faceDetectionEffect;
```

- Add the following method to your code:

```c#
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
```

>  This method leverages the built in capabilities of Windows 10 to detect faces in images.
> 	- **_faceDetectionEffect.DesiredDetectionInterval** - this is how frequently it will check for faces
> 	- **_faceDetectionEffect.FaceDetected += FaceDetectionEffect_FaceDetected;** - This method gets called when a face is detected.



- Then make sure you call *CreateFaceDetectionEffectAsync* at the bottom of your OnNavigatedTo method:

```c#
	protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
		...other code ...
		await CreateFaceDetectionEffectAsync();
	}	
	
```

- Next we need to define *FaceDetectionEffect_FaceDetected*, as mentioned above this method gets called when faces are detected.

```c#
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
					
					//string json = JsonConvert.SerializeObject(candidates, Formatting.Indented);
                    //ResultText.Text = json;
                }
                finally
                {
                    _faceDetectionEffect.FaceDetected += FaceDetectionEffect_FaceDetected;
                }
            });
        }
```
> This code executes on a seperate thread.  So we need to use the Dispatcher to marshall our code to run on the UI thread, otherwise we will get errors.

> Also notice we use "-=" and "+=" to remove the event listener temporarily (until we are done), so that the event doesn't fire again until the frame has been analyzed,

**Run the Application**

Run the application.  You should still get the same video preview, but if you watch the debug window, it should list how many faces are detected in the image.

### Step 5: Capture Frame 

The next step is to capture the frame as an image when a face is detected.  

Add the following using statements:

```c#
using Windows.UI.Xaml.Media.Imaging;
using Windows.Media;
using Windows.Graphics.Imaging;
using Windows.Media.MediaProperties;
```

Then add the following method...

```c#
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
```

This uses the _mediaCapture object to grab the current frame and stuff it into a writeable bitmap.

Edit your FaceDetectionEffect_FaceDetected method, and call your new GetWriteableBitmapFromPreviewFrame method.

```c#
	private async void FaceDetectionEffect_FaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
			... other code ...
			// Do stuff here
			var bmp = await GetWriteableBitmapFromPreviewFrame();
	}
```

###Step 6: Save image to Pictures Library

Now we want to save the image to the pictures library.  

- First we need to add a nuget package:

1. Right-click on your project and select "Manage NuGet Packages"
2. Select the "Browse" tab, and in the search box type: "WriteableBitmapEx"
3. Select the package and install it.

Now add the following method:

```c#
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
```

This method returns a StorageFile. 

Edit your FaceDetectionEffect_FaceDetected method, and call your new method:

```c#
	private async void FaceDetectionEffect_FaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
			... other code ...
			// Do stuff here
			var bmp = await GetWriteableBitmapFromPreviewFrame();
			var file =  = await SaveBitmapToStorage(bmp);		
	}
```

**Run the application**

Open you Pictures folder and run the application.  If you did everything correctly, you should see a new image named "_photo.jpg" show up every time a frame is captured that has a face.  

###Step 7: Add Cognitive Services


	
  
