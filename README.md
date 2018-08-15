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





  
