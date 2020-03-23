using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
//using Grpc.Core; //https://github.com/grpc/grpc/tree/master/src/csharp/experimental
//using Tensorflow.Serving;
//using TensorFlowServing.Utils;



public class ImageAnalyser : MonoBehaviour
{

    /// <summary>
    /// Unique instance of this class
    /// </summary>
    public static ImageAnalyser Instance;

    /// <summary>
    /// Insert your prediction endpoint here
    /// </summary>
    private string predictionEndpoint = "http://35.206.175.230:80/v1/models/holoselecta:predict";

    /// <summary>
    /// Insert your prediction endpoint here
    /// </summary>
    bool use_grpc = false;

    /// <summary>
    /// the buffer of serialized raw request
    /// </summary>
    public Queue<Outputs> outputsBuffer = new Queue<Outputs>();

    /// <summary>
    /// flag to tell others not to pop from rawreqest buffer
    /// </summary>
    public bool outputsBufferEmpty = true;

    /// <summary>
    /// flag to tell imageserializer to refill rawrequest buffer
    /// </summary>
    public bool outputsBufferRefill = true;

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
        if (outputsBufferRefill && !ImageSerializer.Instance.rawRequestBufferEmpty && stopwatch.ElapsedMilliseconds - timestamp > 200)
        {
            //For Timing Analysis
            UnityEngine.Debug.Log($"ImageAnalysis Idletime {stopwatch.ElapsedMilliseconds - timestamp} Runtime {timestamp}");
            stopwatch = Stopwatch.StartNew();

            //Deqeue a rawrequest element from the buffer for the Worker
            RequestBufferElem rawRequestElem = ImageSerializer.Instance.rawRequestBuffer.Dequeue();
            ImageSerializer.Instance.rawRequestBufferRefill = true;
            ImageSerializer.Instance.rawRequestBufferEmpty = true;
            outputsBufferRefill = false;

            //Start Analysis Worker
            StartCoroutine(AnalyseLastImageCaptured(rawRequestElem));
        }
    }

    /// <summary>
    /// Call the Computer Vision Service to submit the image.
    /// </summary>
    public IEnumerator AnalyseLastImageCaptured(RequestBufferElem rawRequestElem)
    {

        // define header as json
        //Dictionary<string, string> headers = new Dictionary<string, string>();
        //headers.Add("Content-Type", "application/json");

        DetectionResponse detectionResponse = new DetectionResponse();
        //send of request
        //using (WWW www = new WWW(predictionEndpoint, rawRequest, headers))
        //{
        //    yield return www;

        //    //Debug.Log($"Response Error {www.error}");
        //    //Debug.Log(www.text);

        //    detectionResponse = JsonConvert.DeserializeObject<DetectionResponse>(www.text);
        //}

        WWWForm webForm = new WWWForm();

        using (UnityWebRequest unityWebRequest = UnityWebRequest.Post(predictionEndpoint, webForm))
        {
            //Set headers
            unityWebRequest.SetRequestHeader("Content-Type", "application/json");

            //Upload Handler to handle data
            unityWebRequest.uploadHandler = new UploadHandlerRaw(rawRequestElem.rawRequest);
            unityWebRequest.uploadHandler.contentType = "application/json";

            // The download handler will help receiving the analysis from Azure
            unityWebRequest.downloadHandler = new DownloadHandlerBuffer();

            // Send the request
            yield return unityWebRequest.SendWebRequest();

            //Decode the json response
            detectionResponse = JsonConvert.DeserializeObject<DetectionResponse>(unityWebRequest.downloadHandler.text);
        }

        //check if the response was succesful
        if (detectionResponse.outputs != null)
        {
            //// Uncomment this to debug the values in the response
            //detectionResponse.outputs = ValuesForCalibration(detectionResponse.outputs);

            //// uncomment this to debug the values in the response
            //DebugLogResponse(detectionResponse.outputs);

            //If world data is available get it
            if (rawRequestElem.hasWorldData)
            {
                detectionResponse.outputs.hasWorldData = true;
                detectionResponse.outputs.cameraToWorld = rawRequestElem.cameraToWorld;
                detectionResponse.outputs.projection = rawRequestElem.projection;
            }
            else
            {
                detectionResponse.outputs.hasWorldData = false;
            }

            //enquue the response and quit worker
            outputsBuffer.Enqueue(detectionResponse.outputs);
            outputsBufferEmpty = false;

            //For Timining Analysis
            timestamp = stopwatch.ElapsedMilliseconds;
        }
        else
        {
            // The Server failed, Show with cursor
            SceneOrganizer.Instance.cursor.GetComponent<Renderer>().material.color = Color.blue;
            // Write a debug message
            UnityEngine.Debug.Log("The Server failed");
            // Hibernate the detection for 2sec
            timestamp = stopwatch.ElapsedMilliseconds + 1800;
        }
    }

    private void DebugLogResponse(Outputs outputs)
    {
        UnityEngine.Debug.Log("------------------------------------");
        double[,] scores10 = new double[1, 10];
        Array.Copy(outputs.detection_scores, 0, scores10, 0, 10);
        double[,] classes10 = new double[1, 10];
        Array.Copy(outputs.detection_classes, 0, classes10, 0, 10);
        double[,,] boxes10 = new double[1, 10, 4];
        Array.Copy(outputs.detection_boxes, 0, boxes10, 0, 40);
        for (int n = 0; n < 10; n++)
        {
            UnityEngine.Debug.Log(scores10[0, n]);
            UnityEngine.Debug.Log(classes10[0, n]);
            UnityEngine.Debug.Log(boxes10[0, n, 0] + " , " + boxes10[0, n, 1] + " , " + boxes10[0, n, 2] + " , " + boxes10[0, n, 3]);
        }
        UnityEngine.Debug.Log("------------------------------------");
    }

    private Outputs ValuesForCalibration()
    {
        Outputs outputs = new Outputs();
        outputs.detection_boxes = new float[1, 9, 4];
        outputs.detection_scores = new float[1, 9];
        outputs.detection_classes = new float[1, 9];
        float[] pos_l = { 0.3f, 0.5f, 0.7f };
        float[] l_var = { 0.025f, 0.05f, 0.1f };
        float[] pos_h = { 0.1f, 0.5f, 0.9f };
        float[] h_var = { 0.025f, 0.05f, 0.1f };
        int cnt = 0;
        for (int l = 0; l < pos_l.Length; l++)
        {
            for (int h = 0; h < pos_h.Length; h++)
            {
                outputs.detection_scores[0, cnt] = 0.99f;
                outputs.detection_classes[0, cnt] = 16.0f; //Capri Sun
                outputs.detection_boxes[0, cnt, 0] = pos_h[h] - h_var[h];
                outputs.detection_boxes[0, cnt, 1] = pos_l[l] - l_var[l];
                outputs.detection_boxes[0, cnt, 2] = pos_h[h] + h_var[h];
                outputs.detection_boxes[0, cnt, 3] = pos_l[l] + l_var[l];
                cnt++;
            }
        }
        return outputs;
    }

    private void RequestGrpc()
    {
        //gRPC doesn't compile on properly assuming that the .NET Standard 2 needs to be enabled in the project
        // not sure how though

        //string ipAddress = "34.76.251.244"; // IP address of TF-Server
        //string port = "443"; // port of TF-Server
        //Channel tf_channel = new Channel(ipAddress, Convert.ToInt32(port), ChannelCredentials.Insecure);
        //PredictionService.PredictionServiceClient tf_client = new PredictionService.PredictionServiceClient(tf_channel);
        //byte[] imageData = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        //var request = new PredictRequest()
        //{
        //    ModelSpec = new ModelSpec()
        //    {
        //        Name = "inception",
        //        SignatureName = "predict_images"
        //    }
        //};
        //int[] shape = { 1, ImageCapture.Instance.height, ImageCapture.Instance.width, 3 };
        //var imageTensor = TensorProtoBuilder.TensorProtoFromImage(imageData, shape);

        //// Add image tensor to request
        //request.Inputs.Add("inputs", imageTensor);

        //PredictResponse predictResponse = tf_client.Predict(request);
        //// Decode response
        //var classesTensor = predictResponse.Outputs["classes"];
        //float[] classes = TensorProtoDecoder.TensorProtoToFloatArray(classesTensor);
        //var boxesTensor = predictResponse.Outputs["boxes"];
        //float[] boxes = TensorProtoDecoder.TensorProtoToFloatArray(boxesTensor);
        //var scoresTensor = predictResponse.Outputs["scores"];
        //float[] scores = TensorProtoDecoder.TensorProtoToFloatArray(scoresTensor);

        //Debug.Log(classes[0]);
        //Debug.Log(scores[0]);
        //Debug.Log(boxes[0] + " - " + boxes[1] + " - " + boxes[2] + " - " + boxes[3]);
    }
}
