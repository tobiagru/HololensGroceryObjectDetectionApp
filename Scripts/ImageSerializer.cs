using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.XR.WSA.WebCam;
using Newtonsoft.Json;

public class RequestBufferElem
{
    public byte[] rawRequest { get; set; }
    public bool hasWorldData { get; set; }
    public Matrix4x4 cameraToWorld = new Matrix4x4();
    public Matrix4x4 projection = new Matrix4x4();
}

public class ImageSerializer : MonoBehaviour {
    /// <summary>
    /// Unique instance of this class
    /// </summary>
    public static ImageSerializer Instance;

    /// <summary>
    /// the buffer of serialized raw request
    /// </summary>
    public Queue<RequestBufferElem> rawRequestBuffer = new Queue<RequestBufferElem>();

    /// <summary>
    /// flag to tell others not to pop from rawreqest buffer
    /// </summary>
    public bool rawRequestBufferEmpty = true;

    /// <summary>
    /// flag to tell imageserializer to refill rawrequest buffer
    /// </summary>
    public bool rawRequestBufferRefill = true;

    private Stopwatch stopwatch = Stopwatch.StartNew();
    private long timestamp = 0;

    /// <summary>
    /// Initializes this class
    /// </summary>
    private void Awake()
    {
        // Allows this instance to behave like a singleton
        Instance = this;
    }

    void Update()
    {

        if (rawRequestBufferRefill && !ImageCapture.Instance.photoFrameBufferEmpty)
        {
            UnityEngine.Debug.Log($"ImageSerializer Idletime {stopwatch.ElapsedMilliseconds - timestamp} Runtime {timestamp}");
            stopwatch = Stopwatch.StartNew();

            PhotoCaptureFrame photoFrame = ImageCapture.Instance.photoFrameBuffer.Dequeue();
            ImageCapture.Instance.photoFrameBufferRefill = true;
            ImageCapture.Instance.photoFrameBufferEmpty = true;
            rawRequestBufferRefill = false;
            StartCoroutine(SerializeRequest(photoFrame));
        }
    }

    /// <summary>)
    /// Serialize the image that was taken and turn it into a rawrequest.
    /// 1. Take photo and decode it as jpeg string string
    /// 2. decode the jpeg wtih base64 to be serializeable
    /// 3. serialize everything as json string
    /// 4. serialize the json string as raw request
    /// </summary>
    private IEnumerator<bool> SerializeRequest(PhotoCaptureFrame photoCapturedFrame)
    {
        yield return true;
        //Texture2D tex = new Texture2D(ImageCapture.Instance.width,
        //                              ImageCapture.Instance.height);
        //photoCapturedFrame.UploadImageDataToTexture(tex);
        //byte[] jpgEncoded = tex.EncodeToJPG

        List<byte> jpgEncodedList = new List<byte>();
        photoCapturedFrame.CopyRawImageDataIntoBuffer(jpgEncodedList);
        byte[] jpgEncoded = jpgEncodedList.ToArray();

        

        // server expects an base64 encoded JPG encoded string
        // should have the form {"inputs": [{"b64": <b64encodejpgencodedstring>}]}
        string b64Encode = Convert.ToBase64String(jpgEncoded);
        DetectionRequest detectionRequest = new DetectionRequest { inputs = new List<B64> { new B64 { b64 = b64Encode } } };

        string jsonRequest = JsonConvert.SerializeObject(detectionRequest);

        RequestBufferElem requestBufferElem = new RequestBufferElem() { rawRequest = Encoding.UTF8.GetBytes(jsonRequest) };
        if(!photoCapturedFrame.TryGetCameraToWorldMatrix(out requestBufferElem.cameraToWorld) || 
           !photoCapturedFrame.TryGetProjectionMatrix(out requestBufferElem.projection))
        {
            requestBufferElem.hasWorldData = false;
        }
        else
        {
            requestBufferElem.hasWorldData = true;
        }

        photoCapturedFrame.Dispose();

        rawRequestBuffer.Enqueue(requestBufferElem);
        rawRequestBufferEmpty = false;

        timestamp = stopwatch.ElapsedMilliseconds;
    }
}
