using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.WSA.Input;
using UnityEngine.XR.WSA.WebCam;


public class ImageCapture : MonoBehaviour {

    /// <summary>
    /// Allows this class to behave like a singleton
    /// </summary>
    public static ImageCapture Instance;

    /// <summary>
    /// the camera resolution
    /// 2048x1152
    /// 1280x720
    /// 1408x792
    /// 1344x756
    /// 896x504
    /// </summary>
    public int width = 1280;
    public int height = 720;

    /// <summary>
    /// PhotoCaptureFrame object (the image with some important camera parameters)
    /// </summary>
    public PhotoCaptureFrame photoCaptureFrame = null;

    /// <summary>
    /// Photo Capture object
    /// </summary>
    private PhotoCapture photoCaptureObject = null;

    /// <summary>
    /// Allows gestures recognition in HoloLens
    /// </summary>
    private GestureRecognizer recognizer;

    /// <summary>
    /// Flagging if the capture loop is running
    /// </summary>
    internal bool captureIsActive = true;

    /// <summary>
    /// the buffer of photoframes
    /// </summary>
    public Queue<PhotoCaptureFrame> photoFrameBuffer = new Queue<PhotoCaptureFrame>();

    /// <summary>
    /// flag to tell others not to pop from the photoFrameBuffer
    /// </summary>
    public bool photoFrameBufferEmpty = true;

    /// <summary>
    /// flag to tell ImageCapture to refill buffer
    /// </summary>
    public bool photoFrameBufferRefill = true;

    private Stopwatch stopwatch = Stopwatch.StartNew();
    private long timestamp = 0;

    /// Called on initialization
    /// </summary>
    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Runs at initialization right after Awake method
    /// </summary>
    void Start()
    {
        // Subscribing to the Microsoft HoloLens API gesture recognizer to track user gestures
        //recognizer = new GestureRecognizer();
        //recognizer.SetRecognizableGestures(GestureSettings.Tap);
        //recognizer.Tapped += TapHandler;
        //recognizer.StartCapturingGestures();

        //Create the camera instance
        PhotoCapture.CreateAsync(true, delegate( PhotoCapture captureObject)
        {
            photoCaptureObject = captureObject;
            captureIsActive = false;
        });
    }

    /// <summary>
    /// Respond to Tap Input.
    /// </summary>
    //private void TapHandler(TappedEventArgs obj)
    //{
    //    stopwatch = Stopwatch.StartNew();
    //    UnityEngine.Debug.Log($"8: TapHandler Start at {stopwatch.ElapsedMilliseconds}ms");

    //    if (!captureIsActive)
    //    {
    //        captureIsActive = true;

    //        // Set the cursor color to red
    //        SceneOrganizer.Instance.cursor.GetComponent<Renderer>().material.color = Color.red;

    //        // let the camera take an image and save it in memory until used
    //        TakePhoto();
    //    }

    //    UnityEngine.Debug.Log($"8: Finished at {stopwatch.ElapsedMilliseconds}ms");
    //}

    void Update()
    {
        if (photoFrameBufferRefill && !captureIsActive)
        {
            UnityEngine.Debug.Log($"ImageCapture Idletime {stopwatch.ElapsedMilliseconds - timestamp} Runtime {timestamp}");
            stopwatch = Stopwatch.StartNew();

            photoFrameBufferRefill = false;
            captureIsActive = true;
            TakePhoto();
        }
    }

    /// <summary>
    /// Setup the PhotoCapture with the relevant parameters
    /// </summary>
    void TakePhoto()
    {
        CameraParameters camParameters = new CameraParameters
        {
            hologramOpacity = 0.0f,
            cameraResolutionWidth = width,
            cameraResolutionHeight = height,
            //pixelFormat = CapturePixelFormat.BGRA32
            pixelFormat = CapturePixelFormat.JPEG
        };

        // initialize the camera
        photoCaptureObject.StartPhotoModeAsync(camParameters, delegate (PhotoCapture.PhotoCaptureResult result)
        {
            photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
        });
    }

    /// <summary>
    /// Register the full execution of the Photo Capture. 
    /// </summary>
    void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCapturedFrameTmp)
    {

        // Create a label in world space using the ResultsLabel class 
        // Invisible at this point but correctly positioned where the image was taken
        SceneOrganizer.Instance.PlaceAnalysisLabel();

        photoFrameBuffer.Enqueue(photoCapturedFrameTmp);
        photoFrameBufferEmpty = false;

        photoCaptureObject.StopPhotoModeAsync(delegate (PhotoCapture.PhotoCaptureResult result_tmp)
        {
            captureIsActive = false;
            timestamp = stopwatch.ElapsedMilliseconds;
        });
    }



    /// <summary>
    /// Stops all capture pending actions
    /// </summary>
    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {

            photoCaptureObject.Dispose();
            photoCaptureObject = null;

            captureIsActive = false;

            // Stop the capture loop if active
            CancelInvoke();

        }
    }
}
