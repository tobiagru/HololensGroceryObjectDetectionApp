using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class SceneOrganizer : MonoBehaviour {

    /// <summary>
    /// Allows this class to behave like a singleton
    /// </summary>
    public static SceneOrganizer Instance;

    /// <summary>
    /// The cursor object attached to the Main Camera
    /// </summary>
    public GameObject cursor;

    /// <summary>
    /// The cursor object attached to the Main Camera
    /// </summary>
    public GameObject box_prefab;

    /// <summary>
    /// The label used to display the analysis on the objects in the real world
    /// </summary>
    public GameObject label_prefab;

    /// <summary>
    /// The list of all labels that are currently drawn
    /// </summary
    internal List<GameObject> boxes = new List<GameObject>();
    internal List<GameObject> labels = new List<GameObject>();
    internal List<float[]> bounds = new List<float[]>();

    /// <summary>
    /// Current threshold accepted for displaying the label
    /// Reduce this value to display the recognition more often
    /// </summary>
    internal float probabilityThreshold = 0.8f;

    /// <summary>
    /// The quad object hosting the imposed image captured
    /// </summary>
    private float local_scale_sizer = 0.008f;

    /// <summary>
    /// The quad object hosting the imposed image captured
    /// </summary>
    private GameObject quad;

    /// <summary>
    /// Renderer of the quad object
    /// </summary>
    internal Renderer quadRenderer;

    /// <summary>
    /// the head direction towards the quad
    /// </summary>
    internal Vector3 headPosition;

    private Stopwatch stopwatch = Stopwatch.StartNew();
    private long timestamp = 0;


    /// <summary>
    /// Called on initialization
    /// </summary>
    private void Awake()
    {
        // Use this class instance as singleton
        Instance = this;

        // Add the ImageCapture class to this Gameobject
        gameObject.AddComponent<ImageCapture>();

        // Add the ImageSerializer class to this Gameobject
        gameObject.AddComponent<ImageSerializer>();

        // Add the ImageAnalyser class to this Gameobject
        gameObject.AddComponent<ImageAnalyser>();

        // Add the MiscObjects class to this Gameobject
        gameObject.AddComponent<MiscObjects>();

    }

    void Update()
    {
        if (!ImageAnalyser.Instance.outputsBufferEmpty)
        {
            if (LoadingIcon.Instance.gameObject.activeSelf)
            {
                LoadingIcon.Instance.gameObject.SetActive(false);
            }

            UnityEngine.Debug.Log($"SceneOrganizer Idletime {stopwatch.ElapsedMilliseconds - timestamp} Runtime {timestamp}");
            stopwatch = Stopwatch.StartNew();

            Outputs outputs = ImageAnalyser.Instance.outputsBuffer.Dequeue();
            ImageAnalyser.Instance.outputsBufferEmpty = true;
            ImageAnalyser.Instance.outputsBufferRefill = true;
            //TODO turn this async (depending on timing)
            StartCoroutine(FinaliseLabel(outputs));
        }
    }

    /// <summary>
    /// Instantiate a Label in the appropriate location relative to the Main Camera.
    /// </summary>
    public void PlaceAnalysisLabel()
    {

        // Create a GameObject to which the texture can be applied
        quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadRenderer = quad.GetComponent<Renderer>() as Renderer;
        Material m = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
        quadRenderer.material = m;

        // Here you can set the transparency of the quad. Useful for debugging
        float transparency = 0.0f;
        quadRenderer.material.color = new Color(1, 1, 1, transparency);

        // Set the position and scale of the quad depending on user position
        quad.transform.parent = transform;
        quad.transform.rotation = transform.rotation;

        // The quad is positioned slightly forward in font of the user
        // x = Breite, y = Höhe --> x++ nach Rechts verschieben, y++ nach oben schieben
        quad.transform.localPosition = new Vector3(0.11f, -0.08f, 3.0f);

        // The quad scale as been set with the following value following experimentation,  
        // to allow the image on the quad to be as precisely imposed to the real world as possible
        // x = Breite, y = Höhe --> x++ nach Breiter machen, y++ Höher machen
        quad.transform.localScale = new Vector3(2.5f, 1.4f, 1f);
        quad.transform.parent = null;

        //TODO catch the headposition at this time
        headPosition = Camera.main.transform.position;

    }

    /// <summary>
    /// Set the Tags as Text of the last label created. 
    /// </summary>
    public IEnumerator<bool> FinaliseLabel(Outputs outputs)
    {
        yield return true;

        // detection scores come sorted from highest to lowest
        for (int i = 0; i < outputs.detection_scores.Length; i++)
        {
            //Debug.Log("-----------------------------");
            //TODO test if an intermediate variable might speed this up
            if (outputs.detection_scores[0, i] > probabilityThreshold)
            {
                // Get all the information about the product
                ProductDefinition productDefinition = GetProductNutri(outputs.detection_classes[0, i]);

                //UnityEngine.Debug.Log($"---- {productDefinition.name} ----");

                // convert box to shape useful to rest of funtions
                // TODO refactor this and all methods that use it to use a class with left, right, bottom, top properties
                // top, left, bottom, right
                float[] bndbox = { outputs.detection_boxes[0, i, 0],
                                   outputs.detection_boxes[0, i, 1],
                                   outputs.detection_boxes[0, i, 2],
                                   outputs.detection_boxes[0, i, 3] };

                //extract two corners
                Vector2 left_top = new Vector2(outputs.detection_boxes[0, i, 1], outputs.detection_boxes[0, i, 0]);
                Vector2 right_bottom = new Vector2(outputs.detection_boxes[0, i, 3], outputs.detection_boxes[0, i, 2]);

                ////calculate scale of the box
                //Vector3 labelLocalScale = new Vector3(0.005f, 0.005f, 0.005f);
                //Vector3 boxLocalScale = CalculateBoxScale(left_top, right_bottom);

                GameObject label = CreateNewLabel(productDefinition, bndbox);
                GameObject box = CreateNewBox(productDefinition, bndbox);

                //get position of the objects
                Vector3 finalLabelPosition;
                Vector3 finalBoxPosition;

                if (outputs.hasWorldData)
                {
                    if (GetFinalPosition(left_top, outputs.cameraToWorld, outputs.projection, out finalLabelPosition) &&
                    GetFinalPosition(right_bottom, outputs.cameraToWorld, outputs.projection, out finalBoxPosition))
                    {
                        label.transform.position = finalLabelPosition;
                        box.transform.position = finalBoxPosition;
                    }
                    else
                    {
                        Destroy(box);
                        Destroy(label);
                        continue;
                    }
                }
                else
                {
                    UnityEngine.Debug.Log("No World Data, switching to Fallback Localization");
                    if (GetFinalPosition(label.transform.position, headPosition, out finalLabelPosition) &&
                        GetFinalPosition(box.transform.position, headPosition, out finalBoxPosition))
                    {
                        label.transform.position = finalLabelPosition;
                        box.transform.position = finalBoxPosition;
                    }
                    else
                    {
                        Destroy(box);
                        Destroy(label);
                        continue;
                    }
                }



                //Vector3 finalLabelPositionTmp;
                //Vector3 finalBoxPositionTmp;
                //GetFinalPosition(left_top, outputs.cameraToWorld, outputs.projection, out finalLabelPositionTmp);
                //GetFinalPosition(right_bottom, outputs.cameraToWorld, outputs.projection, out finalBoxPositionTmp);

                // check if this is a new and should be kept or not
                //IsBoxNew(finalLabelPosition, finalBoxPosition, box.transform.localScale)
                if (IsBoxNew(box, label))
                {
                    // Create new boxes and labels
                    //GameObject label = CreateNewLabel(productDefinition, finalLabelPosition, labelLocalScale);
                    //GameObject box = CreateNewBox(productDefinition, finalBoxPosition, boxLocalScale);

                    // Add them to the List
                    boxes.Add(box);
                    labels.Add(label);
                }
                else
                {
                    Destroy(box);
                    Destroy(label);
                }

            }
            else
            {
                break;
            }
        }

        // Reset the color of the cursor
        cursor.GetComponent<Renderer>().material.color = Color.green;

        timestamp = stopwatch.ElapsedMilliseconds;
    }

    /// <summary>
    /// Calculate box scale
    /// </summary>
    private Vector3 CalculateBoxScale(float[] bndbox)
    {
        float height = (bndbox[2] - bndbox[0]);
        float width = (bndbox[3] - bndbox[1]);
        float height_scaler = height / 0.1f;
        float width_scaler = width / 0.1f;
        Bounds quadBounds = quadRenderer.bounds;
        float quadWidth = quadBounds.size.normalized.x + width_scaler * 0.01f;
        float quadHeight = quadBounds.size.normalized.y - height_scaler * 0.01f;
        return new Vector3(width_scaler * local_scale_sizer * quadWidth, height_scaler * local_scale_sizer * quadHeight, local_scale_sizer);
    }

    ///// <summary>
    ///// Calculate box scale
    ///// </summary>
    //private Vector3 CalculateBoxScale(Vector2 left_top, Vector2 right_bottom)
    //{
    //    Vector2 pixelScale = right_bottom - left_top;
    //    Vector2 normalizedScale = pixelScale / 0.1f;
    //    Vector2 quadScale = quadRenderer.bounds.size.normalized;
    //    // What was this good for? Test without
    //    quadScale.x += normalizedScale.x * 0.01f;
    //    quadScale.y -= normalizedScale.y * 0.01f;
    //    return new Vector3(normalizedScale.x * quadScale.x * local_scale_sizer, normalizedScale.y * quadScale.y * local_scale_sizer, local_scale_sizer);
    //}

    /// <summary>
    /// Create new box according to the outputs
    /// </summary>
    private GameObject CreateNewBox(ProductDefinition productDefinition, float[] bndbox)
    {
        GameObject box = Instantiate(box_prefab, cursor.transform.position, transform.rotation);
        box.transform.localScale = CalculateBoxScale(bndbox);
        box.transform.parent = quad.transform;
        box.transform.localPosition = new Vector3(bndbox[3] - 0.5f, 0.5f - bndbox[2], 0);
        box.name = productDefinition.id;
        box.GetComponent<Renderer>().material.color = GetNutriColor(productDefinition.nutri_label);

        return box;
    }

    ///// <summary>
    ///// Create new box according to the outputs
    ///// </summary>
    //private GameObject CreateNewBox(ProductDefinition productDefinition, Vector3 finalPosition, Vector3 localScale)
    //{
    //    GameObject box = Instantiate(box_prefab, cursor.transform.position, transform.rotation);
    //    box.transform.localScale = localScale;
    //    box.transform.parent = quad.transform;
    //    box.transform.position = finalPosition;
    //    box.name = productDefinition.name;
    //    box.GetComponent<Renderer>().material.color = GetNutriColor(productDefinition.nutri_label);

    //    return box;
    //}

    /// <summary>
    /// Create new label according to the outputs
    /// </summary>
    private GameObject CreateNewLabel(ProductDefinition productDefinition, float[] bndbox)
    {
        GameObject label = Instantiate(label_prefab, cursor.transform.position, transform.rotation);
        TextMesh labelText = label.GetComponent<TextMesh>();
        label.transform.localScale = new Vector3(0.005f, 0.005f, 0.005f);
        label.transform.parent = quad.transform;
        label.transform.localPosition = new Vector3(bndbox[1] - 0.5f, 0.5f - bndbox[0], 0);
        labelText.text = productDefinition.nutri_label;
        labelText.fontSize = 28;
        labelText.color = Color.white;

        return label;
    }

    ///// <summary>
    ///// Create new label according to the outputs
    ///// </summary>
    //private GameObject CreateNewLabel(ProductDefinition productDefinition, Vector3 finalPosition, Vector3 localScale)
    //{
    //    GameObject label = Instantiate(label_prefab, cursor.transform.position, transform.rotation);
    //    TextMesh labelText = label.GetComponent<TextMesh>();
    //    label.transform.localScale = localScale;
    //    label.transform.parent = quad.transform;
    //    label.transform.position = finalPosition;
    //    labelText.text = productDefinition.nutri_label;
    //    labelText.fontSize = 28;
    //    labelText.color = Color.white;

    //    return label;
    //}

    /// <summary>
    /// Get the final Position of either Label or Box. 
    /// Cast a ray from the user's head to the current position of the object, it should hit the object detected by the Service.
    /// return the position where where the ray HL sensor collides with the object, or otherwise None.
    /// </summary>
    private bool GetFinalPosition(Vector3 objDirection, Vector3 headPosition, out Vector3 finalPosition)
    {
        RaycastHit objHitInfo;
        if (Physics.Raycast(headPosition, objDirection, out objHitInfo, 30.0f, SpatialMapping.PhysicsRaycastMask))
        {
            finalPosition = objHitInfo.point;
            return true;
        }
        else
        {
            finalPosition = new Vector3(0, 0, 0);
            return false;
        }
    }

    /// <summary>
    /// Get the final Position of either Label or Box. 
    /// </summary>
    private bool GetFinalPosition(Vector2 pxlPos, Matrix4x4 cameraToWorld, Matrix4x4 projection, out Vector3 finalPosition)
    {
        // projected Position normalized within [-1,1] on the image
        Vector3 projPos = new Vector3(pxlPos.x * 2 - 1, 1 - pxlPos.y * 2, 1);
        // w factor to normalize unprojected vector
        float wn = projPos.z / cameraToWorld.m22;
        // unprojected Position from Image to Camera Position via Intrinsics
        Vector4 unProjPos = new Vector4((projPos.x - wn * projection.m02) / projection.m00,
                                        (projPos.y - wn * projection.m12) / projection.m11,
                                        wn,
                                        0);
        //calculate the start Position of the Ray from the camera
        Vector4 cameraRayPos4 = cameraToWorld * new Vector4(0, 0, 0, 1);
        Vector3 cameraRayPos = new Vector3(cameraRayPos4.x, cameraRayPos4.y, cameraRayPos4.z);
        // calculate the direction of the Ray from the unprojected Position
        Vector4 unProjRayPos4 = cameraToWorld * unProjPos;
        Vector3 unProjRayPos = new Vector3(unProjRayPos4.x, unProjRayPos4.y, unProjRayPos4.z);

        //Do a RayCast from camera through Projected Position onto the wall and assign as final Position
        RaycastHit objHitInfo;
        if (Physics.Raycast(cameraRayPos, unProjRayPos, out objHitInfo, 30.0f, SpatialMapping.PhysicsRaycastMask))
        {
            finalPosition = objHitInfo.point;
            return true;
        }
        else
        {
            finalPosition = new Vector3(0, 0, 0);
            return false;
        }
    }

    /// <summary>
    /// check for overlap with other boxes of same type (currently only in 2D), 
    ///     if yes for same name above thresh updates the existing and return false
    ///     if yes below update thresh disregard the newbox and return false
    ///     else returns true
    /// </summary>
    private bool IsBoxNew(Vector3 newBox, Vector3 newLabel, Vector3 newScale)
    {
        Vector2 newSize = (newBox - newLabel) / 2;
        Vector2 newMid = newBox + 0.5f * (newLabel - newBox);

        for (int i = 0; i < boxes.Count; i++)
        {
            Vector2 oldSize = (boxes[i].transform.position - labels[i].transform.position) / 2;
            Vector2 oldMid = boxes[i].transform.position + 0.5f * (labels[i].transform.position - boxes[i].transform.position);

            Vector2 distance = oldMid - newMid;
            float overlapX = 1 - System.Math.Abs(distance.x) / (System.Math.Abs(newSize.x) + System.Math.Abs(oldSize.x));
            float overlapY = 1 - System.Math.Abs(distance.y) / (System.Math.Abs(newSize.y) + System.Math.Abs(oldSize.y));

            float overlap = overlapX * overlapY;

            // if the overlap is large enough update the existing box
            if (overlapX < 0 || overlapY < 0)
            {
                continue;
            }
            else if (overlap > 0.5f)
            {
                //this.labels[i].transform.position = (newLabel * 0.2f + this.labels[i].transform.position) / 1.2f;
                //this.boxes[i].transform.position = (newBox * 0.2f + this.boxes[i].transform.position) / 1.2f;
                //this.boxes[i].transform.localScale = (newBox * 0.2f + this.boxes[i].transform.localScale) / 1.2f;
            }
            // if the overlap is not small enough this is not a new box
            if (overlap > 0.1f)
            {
                return false;
            }
        }
        return true;
    }


    /// <summary>
    /// check for overlap with other boxes of same type (currently only in 2D), 
    ///     if yes for same name above thresh updates the existing and return false
    ///     if yes for different name or below thresh update the existing and the newbndbox to follow divider and return true
    ///     else returns true
    /// </summary>
    private bool IsBoxNew(GameObject newbox, GameObject newlabel)
    {
        //Todo convert all of this to pointers, this is horrible

        float new_left = newlabel.transform.position[0] + 1.0f;
        float new_top = newlabel.transform.position[1] + 1.0f;
        float new_right = newbox.transform.position[0] + 1.0f;
        float new_bottom = newbox.transform.position[1] + 1.0f;
        float new_width = new_right - new_left;
        float new_height = new_top - new_bottom;
        //UnityEngine.Debug.Log($"new l:{new_left} r:{new_right} b:{new_bottom} t:{new_top} - - Scale x:{newbox.transform.localScale.x} y:{newbox.transform.localScale.y} z:{newbox.transform.localScale.z}");

        float xOverlap;
        float yOverlap;
        for (int i=0; i < boxes.Count; i++)
        {
            float old_left = labels[i].transform.position[0] + 1.0f;
            float old_top = labels[i].transform.position[1] + 1.0f;
            float old_right = boxes[i].transform.position[0] + 1.0f;
            float old_bottom = boxes[i].transform.position[1] + 1.0f;
            if (new_left > old_left)
            {
                if (new_right < old_right)
                {
                    xOverlap = 1.0f;
                }
                else
                {
                    xOverlap = (old_right - new_left) / new_width;
                }
            }
            else
            {
                if (new_right > old_right)
                {
                    xOverlap = 1.0f;
                }
                else
                {
                    xOverlap = (new_right - old_left) / new_width;
                }
            }
            if (xOverlap < 0)
            {
                //Debug.Log($"old l:{old_left} r:{old_right} b:{old_bottom} t:{old_top} - {boxes[i].name} -  xO:{xOverlap} ");
                continue;
            }

            if (new_bottom > old_bottom)
            {
                if (new_top < old_top)
                {
                    yOverlap = 1.0f;
                }
                else
                {
                    yOverlap = (old_top - new_bottom) / new_height;
                }
            }
            else
            {
                if (new_top > old_top)
                {
                    yOverlap = 1.0f;
                }
                else
                {
                    yOverlap = (new_top - old_bottom) / new_height;
                }
            }
            if (yOverlap < 0)
            {
                //Debug.Log($"old l:{old_left} r:{old_right} b:{old_bottom} t:{old_top} - {boxes[i].name} -  xO:{xOverlap} yO:{yOverlap} O:{xOverlap * yOverlap}");
                continue;
            }
            //UnityEngine.Debug.Log($"old l:{old_left} r:{old_right} b:{old_bottom} t:{old_top} - Scale x:{this.boxes[i].transform.localScale.x} y:{this.boxes[i].transform.localScale.y} z:{this.boxes[i].transform.localScale.z} - {boxes[i].name} -  xO:{xOverlap} yO:{yOverlap} O:{xOverlap * yOverlap}");
            if (xOverlap * yOverlap > 0.5f)
            {
                // Update the existing box by a moving average of 0.2 from the new position
                this.labels[i].transform.position = (newlabel.transform.position * 0.2f + this.labels[i].transform.position) / 1.2f;
                this.boxes[i].transform.position = (newbox.transform.position * 0.2f + this.boxes[i].transform.position) / 1.2f;
                this.boxes[i].transform.localScale = (newbox.transform.localScale * 0.2f + this.boxes[i].transform.localScale) / 1.2f;
                //UnityEngine.Debug.Log($"uol l:{this.labels[i].transform.position.x} r:{this.boxes[i].transform.position.x} b:{this.boxes[i].transform.position.y} t:{this.labels[i].transform.position.y} - Scale x:{this.boxes[i].transform.localScale.x} y:{this.boxes[i].transform.localScale.y} z:{this.boxes[i].transform.localScale.z}");
            }
            if (xOverlap * yOverlap > 0.1f)
            {
                return false;
            }
        }
        return true;
    }


    /// <summary>
    /// get the product definition for a class_id
    /// </summary>
    public ProductDefinition GetProductNutri(double obj_id)
    {
        // For now the classes are just hardcoded. Maybe in the future call them from eatfit API
        ProductDefinition productDefinition = new ProductDefinition();
        switch ((int)obj_id)
        {
            case 1:
                productDefinition.id = "1";
                productDefinition.name = "mnms_gelb__45__40111445";
                productDefinition.gtin = "40111445";
                productDefinition.nutri_score = 14;
                productDefinition.nutri_label = "D";
                productDefinition.calories = "230.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "24.1";
                productDefinition.sugarColor = "C";
                productDefinition.fat = "4.6";
                productDefinition.fatColor = "C";
                productDefinition.protein = "4.4";
                productDefinition.proteinColor = "B";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 2:
                productDefinition.id = "2";
                productDefinition.name = "ragusa___50__76401121";
                productDefinition.gtin = "76401121";
                productDefinition.nutri_score = 20;
                productDefinition.nutri_label = "E";
                productDefinition.calories = "576.0";
                productDefinition.caloriesColor = "D";
                productDefinition.sugar = "49.0";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "15.0";
                productDefinition.fatColor = "E";
                productDefinition.protein = "7.0";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "3.0";
                productDefinition.fiberColor = "A";
                break;
            case 3:
                productDefinition.id = "3";
                productDefinition.name = "torino___46__76415272";
                productDefinition.gtin = "76415272";
                productDefinition.nutri_score = 21;
                productDefinition.nutri_label = "E";
                productDefinition.calories = "571.0";
                productDefinition.caloriesColor = "D";
                productDefinition.sugar = "47.0";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "20.0";
                productDefinition.fatColor = "E";
                productDefinition.protein = "8.0";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "2.0";
                productDefinition.fiberColor = "B";
                break;
            case 4:
                productDefinition.id = "4";
                productDefinition.name = "maltesers_teasers__35__5000159462129";
                productDefinition.gtin = "5000159462129";
                productDefinition.nutri_score = 2;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "186.0";
                productDefinition.caloriesColor = "C";
                productDefinition.sugar = "18.5";
                productDefinition.sugarColor = "C";
                productDefinition.fat = "6.3";
                productDefinition.fatColor = "D";
                productDefinition.protein = "2.6";
                productDefinition.proteinColor = "B";
                productDefinition.fiber = "0";
                productDefinition.fiberColor = "C";
                break;
            case 5:
                productDefinition.id = "5";
                productDefinition.name = "kagi___50__7610046000259";
                productDefinition.gtin = "7610046000259";
                productDefinition.nutri_score = 19;
                productDefinition.nutri_label = "E";
                productDefinition.calories = "559.0";
                productDefinition.caloriesColor = "D";
                productDefinition.sugar = "38.0";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "25.0";
                productDefinition.fatColor = "E";
                productDefinition.protein = "6.4";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "3.1";
                productDefinition.fiberColor = "A";
                break;
            case 6:
                productDefinition.id = "6";
                productDefinition.name = "twix___50__5000159459228";
                productDefinition.gtin = "5000159459228";
                productDefinition.nutri_score = 3;
                productDefinition.nutri_label = "C";
                productDefinition.calories = "124.0";
                productDefinition.caloriesColor = "A";
                productDefinition.sugar = "12.2";
                productDefinition.sugarColor = "B";
                productDefinition.fat = "3.5";
                productDefinition.fatColor = "B";
                productDefinition.protein = "1.1";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 8:
                productDefinition.id = "8";
                productDefinition.name = "zweifel_paprika__90__7610095013002";
                productDefinition.gtin = "7610095013002";
                productDefinition.nutri_score = 5;
                productDefinition.nutri_label = "C";
                productDefinition.calories = "544.0";
                productDefinition.caloriesColor = "D";
                productDefinition.sugar = "5.0";
                productDefinition.sugarColor = "A";
                productDefinition.fat = "2.0";
                productDefinition.fatColor = "A";
                productDefinition.protein = "6.0";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "5.0";
                productDefinition.fiberColor = "A";
                break;
            case 9:
                productDefinition.id = "9";
                productDefinition.name = "toffifee___33__4014400924275";
                productDefinition.gtin = "4014400924275";
                productDefinition.nutri_score = 0;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "522.0";
                productDefinition.caloriesColor = "D";
                productDefinition.sugar = "48.9";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "12.7";
                productDefinition.fatColor = "E";
                productDefinition.protein = "6.0";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "0";
                productDefinition.fiberColor = "C";
                break;
            case 11:
                productDefinition.id = "11";
                productDefinition.name = "balisto_muesli__37g__5000159418546";
                productDefinition.gtin = "5000159418546";
                productDefinition.nutri_score = 0;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "93.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "8.1";
                productDefinition.sugarColor = "B";
                productDefinition.fat = "2.0";
                productDefinition.fatColor = "B";
                productDefinition.protein = "1.1";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0";
                productDefinition.fiberColor = "C";
                break;
            case 12:
                productDefinition.id = "12";
                productDefinition.name = "knoppers_riegel____4035800488808";
                productDefinition.gtin = "4035800488808";
                productDefinition.nutri_score = 19;
                productDefinition.nutri_label = "E";
                productDefinition.calories = "535.0";
                productDefinition.caloriesColor = "D";
                productDefinition.sugar = "38.8";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "14.4";
                productDefinition.fatColor = "E";
                productDefinition.protein = "8.4";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 13:
                productDefinition.id = "13";
                productDefinition.name = "bueno___43__4008400320328";
                productDefinition.gtin = "4008400320328";
                productDefinition.nutri_score = 12;
                productDefinition.nutri_label = "D";
                productDefinition.calories = "122.0";
                productDefinition.caloriesColor = "A";
                productDefinition.sugar = "41.2";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "17.3";
                productDefinition.fatColor = "E";
                productDefinition.protein = "8.6";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 14:
                productDefinition.id = "14";
                productDefinition.name = "darwida_sandwich____7610032065170";
                productDefinition.gtin = "7610032065170";
                productDefinition.nutri_score = 6;
                productDefinition.nutri_label = "C";
                productDefinition.calories = "483.0";
                productDefinition.caloriesColor = "D";
                productDefinition.sugar = "3.0";
                productDefinition.sugarColor = "B";
                productDefinition.fat = "6.0";
                productDefinition.fatColor = "D";
                productDefinition.protein = "10.0";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "5.0";
                productDefinition.fiberColor = "A";
                break;
            case 15:
                productDefinition.id = "15";
                productDefinition.name = "snickers___50__5000159461122";
                productDefinition.gtin = "5000159461122";
                productDefinition.nutri_score = 6;
                productDefinition.nutri_label = "C";
                productDefinition.calories = "241.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "26.0";
                productDefinition.sugarColor = "C";
                productDefinition.fat = "4.0";
                productDefinition.fatColor = "B";
                productDefinition.protein = "4.3";
                productDefinition.proteinColor = "B";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 16:
                productDefinition.id = "16";
                productDefinition.name = "valser_classic__50__76404160";
                productDefinition.gtin = "76404160";
                productDefinition.nutri_score = -15;
                productDefinition.nutri_label = "A";
                productDefinition.calories = "0.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "0.0";
                productDefinition.sugarColor = "A";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "A";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 17:
                productDefinition.id = "17";
                productDefinition.name = "evian___50__3068320353500";
                productDefinition.gtin = "3068320353500";
                productDefinition.nutri_score = -15;
                productDefinition.nutri_label = "A";
                productDefinition.calories = "0.0";
                productDefinition.caloriesColor = "A";
                productDefinition.sugar = "0.0";
                productDefinition.sugarColor = "A";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "A";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 18:
                productDefinition.id = "18";
                productDefinition.name = "volvic_pinapple__50__305764335648";
                productDefinition.gtin = "305764335648";
                productDefinition.nutri_score = 8;
                productDefinition.nutri_label = "C";
                productDefinition.calories = "26.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "6.4";
                productDefinition.sugarColor = "C";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "C";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 20:
                productDefinition.id = "20";
                productDefinition.name = "fuse_peach__50__5449000236623";
                productDefinition.gtin = "5449000236623";
                productDefinition.nutri_score = 6;
                productDefinition.nutri_label = "D";
                productDefinition.calories = "19.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "4.5";
                productDefinition.sugarColor = "B";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "A";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 21:
                productDefinition.id = "21";
                productDefinition.name = "fuse_lemon_dose_33__5449000235947";
                productDefinition.gtin = "5449000235947";
                productDefinition.nutri_score = 4;
                productDefinition.nutri_label = "C";
                productDefinition.calories = "19.0";
                productDefinition.caloriesColor = "A";
                productDefinition.sugar = "4.4";
                productDefinition.sugarColor = "B";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "A";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 22:
                productDefinition.id = "22";
                productDefinition.name = "rivella_rot__50__7610097111072";
                productDefinition.gtin = "7610097111072";
                productDefinition.nutri_score = 8;
                productDefinition.nutri_label = "D";
                productDefinition.calories = "37.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "9.0";
                productDefinition.sugarColor = "D";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "A";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 23:
                productDefinition.id = "23";
                productDefinition.name = "redbull___33__90162909";
                productDefinition.gtin = "90162909";
                productDefinition.nutri_score = 10;
                productDefinition.nutri_label = "E";
                productDefinition.calories = "46.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "11.0";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "A";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 24:
                productDefinition.id = "24";
                productDefinition.name = "valser_vivabirne__50__7610335001530";
                productDefinition.gtin = "7610335001530";
                productDefinition.nutri_score = 0;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "18.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "4.2";
                productDefinition.sugarColor = "C";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "B";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 25:
                productDefinition.id = "25";
                productDefinition.name = "ramseier_jusdepomme__50__7610057001078";
                productDefinition.gtin = "7610057001078";
                productDefinition.nutri_score = 0;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "113.0";
                productDefinition.caloriesColor = "C";
                productDefinition.sugar = "11.0";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "B";
                productDefinition.protein = "0.5";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0";
                productDefinition.fiberColor = "C";
                break;
            case 26:
                productDefinition.id = "26";
                productDefinition.name = "fanta___50__40822938";
                productDefinition.gtin = "40822938";
                productDefinition.nutri_score = 11;
                productDefinition.nutri_label = "E";
                productDefinition.calories = "51.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "12.2";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "A";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 27:
                productDefinition.id = "27";
                productDefinition.name = "mezzomix___50__54490840";
                productDefinition.gtin = "54490840";
                productDefinition.nutri_score = 0;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "43.0";
                productDefinition.caloriesColor = "C";
                productDefinition.sugar = "10.5";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "B";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0";
                productDefinition.fiberColor = "C";
                break;
            case 28:
                productDefinition.id = "28";
                productDefinition.name = "coke_zero_flasche_50__5449000131836";
                productDefinition.gtin = "5449000131836";
                productDefinition.nutri_score = 0;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "0.2";
                productDefinition.caloriesColor = "A";
                productDefinition.sugar = "0.0";
                productDefinition.sugarColor = "A";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "A";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 29:
                productDefinition.id = "29";
                productDefinition.name = "coke__dose_33__54491472";
                productDefinition.gtin = "54491472";
                productDefinition.nutri_score = 10;
                productDefinition.nutri_label = "E";
                productDefinition.calories = "42.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "10.6";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "A";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 31:
                productDefinition.id = "31";
                productDefinition.name = "oreo___154__7622300336738";
                productDefinition.gtin = "7622300336738";
                productDefinition.nutri_score = 20;
                productDefinition.nutri_label = "E";
                productDefinition.calories = "480.0";
                productDefinition.caloriesColor = "D";
                productDefinition.sugar = "38.0";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "9.8";
                productDefinition.fatColor = "D";
                productDefinition.protein = "5.0";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "B";
                break;
            case 32:
                productDefinition.id = "32";
                productDefinition.name = "bifi_roll__50__4251097402635";
                productDefinition.gtin = "4251097402635";
                productDefinition.nutri_score = 10;
                productDefinition.nutri_label = "C";
                productDefinition.calories = "450.0";
                productDefinition.caloriesColor = "C";
                productDefinition.sugar = "3.5";
                productDefinition.sugarColor = "A";
                productDefinition.fat = "14.0";
                productDefinition.fatColor = "E";
                productDefinition.protein = "15.0";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "1.5";
                productDefinition.fiberColor = "B";
                break;
            case 33:
                productDefinition.id = "33";
                productDefinition.name = "c+swiss_dosenabisicetea__33__9120025930135";
                productDefinition.gtin = "9120025930135";
                productDefinition.nutri_score = 9;
                productDefinition.nutri_label = "D";
                productDefinition.calories = "31.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "7.4";
                productDefinition.sugarColor = "B";
                productDefinition.fat = "0";
                productDefinition.fatColor = "B";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 34:
                productDefinition.id = "34";
                productDefinition.name = "caprisun_multivitamin__20__4000177605004";
                productDefinition.gtin = "4000177605004";
                productDefinition.nutri_score = 8;
                productDefinition.nutri_label = "D";
                productDefinition.calories = "39.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "9.0";
                productDefinition.sugarColor = "D";
                productDefinition.fat = "0.1";
                productDefinition.fatColor = "A";
                productDefinition.protein = "0.5";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 35:
                productDefinition.id = "35";
                productDefinition.name = "comella_schokodrink__33__7613100037253";
                productDefinition.gtin = "7613100037253";
                productDefinition.nutri_score = 0;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "66.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "10.0";
                productDefinition.sugarColor = "C";
                productDefinition.fat = "0.6";
                productDefinition.fatColor = "B";
                productDefinition.protein = "4.5";
                productDefinition.proteinColor = "B";
                productDefinition.fiber = "0";
                productDefinition.fiberColor = "C";
                break;
            case 36:
                productDefinition.id = "36";
                productDefinition.name = "valser_still__50__7610335002575";
                productDefinition.gtin = "7610335002575";
                productDefinition.nutri_score = 0;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "0.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "0.0";
                productDefinition.sugarColor = "B";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "B";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 40:
                productDefinition.id = "40";
                productDefinition.name = "redbull_light__33__90162800";
                productDefinition.gtin = "90162800";
                productDefinition.nutri_score = 1;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "3.0";
                productDefinition.caloriesColor = "A";
                productDefinition.sugar = "0.0";
                productDefinition.sugarColor = "A";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "A";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 42:
                productDefinition.id = "42";
                productDefinition.name = "zweifel_graneochilli__100__7610095184009";
                productDefinition.gtin = "7610095184009";
                productDefinition.nutri_score = 8;
                productDefinition.nutri_label = "C";
                productDefinition.calories = "471.0";
                productDefinition.caloriesColor = "C";
                productDefinition.sugar = "7.0";
                productDefinition.sugarColor = "A";
                productDefinition.fat = "1.5";
                productDefinition.fatColor = "A";
                productDefinition.protein = "8.0";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "5.0";
                productDefinition.fiberColor = "A";
                break;
            case 43:
                productDefinition.id = "43";
                productDefinition.name = "jacklinks_beefjerkyorginal__25__4047751730219";
                productDefinition.gtin = "4047751730219";
                productDefinition.nutri_score = 4;
                productDefinition.nutri_label = "C";
                productDefinition.calories = "262.0";
                productDefinition.caloriesColor = "C";
                productDefinition.sugar = "12.0";
                productDefinition.sugarColor = "C";
                productDefinition.fat = "1.7";
                productDefinition.fatColor = "B";
                productDefinition.protein = "42.0";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 45:
                productDefinition.id = "45";
                productDefinition.name = "maltesers___100__5000159023061";
                productDefinition.gtin = "5000159023061";
                productDefinition.nutri_score = 13;
                productDefinition.nutri_label = "D";
                productDefinition.calories = "167.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "17.2";
                productDefinition.sugarColor = "B";
                productDefinition.fat = "5.0";
                productDefinition.fatColor = "C";
                productDefinition.protein = "2.7";
                productDefinition.proteinColor = "B";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 46:
                productDefinition.id = "46";
                productDefinition.name = "lorenz_nicnacs__40__4018077798818";
                productDefinition.gtin = "4018077798818";
                productDefinition.nutri_score = 9;
                productDefinition.nutri_label = "C";
                productDefinition.calories = "540.0";
                productDefinition.caloriesColor = "D";
                productDefinition.sugar = "7.3";
                productDefinition.sugarColor = "A";
                productDefinition.fat = "11.0";
                productDefinition.fatColor = "E";
                productDefinition.protein = "15.0";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "4.4";
                productDefinition.fiberColor = "A";
                break;
            case 47:
                productDefinition.id = "47";
                productDefinition.name = "malburner_partysticks__40__7610200279682";
                productDefinition.gtin = "7610200279682";
                productDefinition.nutri_score = 12;
                productDefinition.nutri_label = "D";
                productDefinition.calories = "490.0";
                productDefinition.caloriesColor = "D";
                productDefinition.sugar = "1";
                productDefinition.sugarColor = "B";
                productDefinition.fat = "14.0";
                productDefinition.fatColor = "E";
                productDefinition.protein = "36.0";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 48:
                productDefinition.id = "48";
                productDefinition.name = "powerbar_proteinplusschoko__55__4029679520028";
                productDefinition.gtin = "4029679520028";
                productDefinition.nutri_score = 0;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "187.0";
                productDefinition.caloriesColor = "C";
                productDefinition.sugar = "16.0";
                productDefinition.sugarColor = "C";
                productDefinition.fat = "2.2";
                productDefinition.fatColor = "C";
                productDefinition.protein = "17.0";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "7.4";
                productDefinition.fiberColor = "A";
                break;
            case 49:
                productDefinition.id = "49";
                productDefinition.name = "airwaves_menthoneucalyptus_riegel_14_1_50173167";
                productDefinition.gtin = "50173167";
                productDefinition.nutri_score = 0;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "142.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "0.0";
                productDefinition.sugarColor = "B";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "B";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0";
                productDefinition.fiberColor = "C";
                break;
            case 51:
                productDefinition.id = "51";
                productDefinition.name = "stimorol_wildcherry_riegel_14_1_57060330";
                productDefinition.gtin = "57060330";
                productDefinition.nutri_score = 0;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "182.0";
                productDefinition.caloriesColor = "C";
                productDefinition.sugar = "0.1";
                productDefinition.sugarColor = "B";
                productDefinition.fat = "0.4";
                productDefinition.fatColor = "B";
                productDefinition.protein = "0.4";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0";
                productDefinition.fiberColor = "C";
                break;
            case 54:
                productDefinition.id = "54";
                productDefinition.name = "fini_galaxymix_packung_100_1_8410525150364";
                productDefinition.gtin = "8410525150364";
                productDefinition.nutri_score = 0;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "320.0";
                productDefinition.caloriesColor = "C";
                productDefinition.sugar = "54.0";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "B";
                productDefinition.protein = "5.0";
                productDefinition.proteinColor = "A";
                productDefinition.fiber = "0";
                productDefinition.fiberColor = "C";
                break;
            case 55:
                productDefinition.id = "55";
                productDefinition.name = "fini_jellykisses_packung_80_1_8410525116704";
                productDefinition.gtin = "8410525116704";
                productDefinition.nutri_score = 0;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "326.4";
                productDefinition.caloriesColor = "C";
                productDefinition.sugar = "58.0";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "B";
                productDefinition.protein = "4.0";
                productDefinition.proteinColor = "B";
                productDefinition.fiber = "0";
                productDefinition.fiberColor = "C";
                break;
            case 56:
                productDefinition.id = "56";
                productDefinition.name = "fuse_lemon_dose_33__5449000235947";
                productDefinition.gtin = "5449000235947";
                productDefinition.nutri_score = 4;
                productDefinition.nutri_label = "C";
                productDefinition.calories = "19.0";
                productDefinition.caloriesColor = "A";
                productDefinition.sugar = "4.4";
                productDefinition.sugarColor = "B";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "A";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 58:
                productDefinition.id = "58";
                productDefinition.name = "coke__dose_33__54491472";
                productDefinition.gtin = "54491472";
                productDefinition.nutri_score = 10;
                productDefinition.nutri_label = "E";
                productDefinition.calories = "42.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "10.6";
                productDefinition.sugarColor = "E";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "A";
                productDefinition.protein = "0.0";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            case 148:
                productDefinition.id = "148";
                productDefinition.name = "volvic_teeminze__50__3057640335648";
                productDefinition.gtin = "3057640335648";
                productDefinition.nutri_score = 0;
                productDefinition.nutri_label = "B";
                productDefinition.calories = "21.0";
                productDefinition.caloriesColor = "B";
                productDefinition.sugar = "4.9";
                productDefinition.sugarColor = "B";
                productDefinition.fat = "0.0";
                productDefinition.fatColor = "B";
                productDefinition.protein = "0.5";
                productDefinition.proteinColor = "C";
                productDefinition.fiber = "0.0";
                productDefinition.fiberColor = "C";
                break;
            default:
                UnityEngine.Debug.Log(string.Format("unknown label {0}", obj_id));
                break;
        }
        return productDefinition;
    }


    /// <summary>
    /// get the corresponding color on the nutrilabel for a nutriletter
    /// </summary>
    public Color GetNutriColor (string nutriLetter)
    {
        switch (nutriLetter)
        {
            case "A":
                return new Color(3.0f / 255, 129.0f / 255, 65.0f / 255, 0.8f);
            case "B":
                return new Color(133.0f / 255, 187.0f / 255, 47.0f / 255, 0.8f);
            case "C":
                return new Color(254.0f / 255, 203.0f / 255, 2.0f / 255, 0.8f);
            case "D":
                return new Color(238.0f / 255, 129.0f / 255, 0.0f / 255, 0.8f);
            case "E":
                return new Color(230.0f / 255, 62.0f / 255, 17.0f / 255, 0.8f);
            default:
                return Color.white;
        }
    }
}
