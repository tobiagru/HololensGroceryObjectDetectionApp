using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;


public class Outputs
{
    // TODO does float make more sense? Response is probably floa anyhow, maybe even float16
    public float[,] detection_scores { get; set; }
    public float[,] detection_classes { get; set; }
    // TODO this needs to be changed for gRPC as the array is 1D
    public float[,,] detection_boxes { get; set; }
    public bool hasWorldData { get; set; }
    public Matrix4x4 cameraToWorld { get; set; }
    public Matrix4x4 projection { get; set; }
}

public class DetectionResponse
{
    public Outputs outputs { get; set; }
}

//public class Inputs
//{
//    //public byte[,,,] inputs { get; set; }
//    public byte[,,,] inputs { get; set; }
//}

public class B64
{
    public string b64 { get; set; }
}

public class DetectionRequest
{
    public List<B64> inputs { get; set; }
}

public class ProductDefinition
{
    public string id { get; set; }
    public string gtin { get; set; }
    public string name { get; set; }
    public int nutri_score { get; set; }
    public string nutri_label { get; set; }
    public string calories { get; set; }
    public string caloriesColor { get; set; }
    public string sugar { get; set; }
    public string sugarColor { get; set; }
    public string fat { get; set; }
    public string fatColor { get; set; }
    public string protein { get; set; }
    public string proteinColor { get; set; }
    public string fiber { get; set; }
    public string fiberColor { get; set; }
}


public class MiscObjects : MonoBehaviour {

}
