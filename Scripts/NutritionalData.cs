using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.WSA.Input;

public class NutritionalData : MonoBehaviour {

    /// <summary>
    /// Allows this class to behave like a singleton
    /// </summary>
    public static NutritionalData Instance;

    /// <summary>
    /// Allows gestures recognition in HoloLens
    /// </summary>
    private GestureRecognizer recognizer;

    // Use this for initialization
    void Awake () {
        Instance = this;

        gameObject.SetActive(false);

        recognizer = new GestureRecognizer();
        recognizer.SetRecognizableGestures(GestureSettings.Tap);
        recognizer.Tapped += TapHandler;
        recognizer.StartCapturingGestures();

    }


    /// <summary>
    /// Handle the TapEvent
    /// </summary>
    private void TapHandler(TappedEventArgs obj)
    {
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
        else
        {
            OrganizeNutriPanel();
        }

    }

    /// <summary>
    /// Handle the TapEvent
    /// </summary>
    private void OrganizeNutriPanel()
    {
        //IEnumerator<bool>
        //yield return true;

        //check if the Cursor looks at an Object
        Vector2 c = SceneOrganizer.Instance.cursor.transform.position;
        for (int i = 0; i < SceneOrganizer.Instance.boxes.Count; i++)
        {
            Vector2 rb = SceneOrganizer.Instance.boxes[i].transform.position;
            Vector2 lt = SceneOrganizer.Instance.labels[i].transform.position;

            if (c.x >= lt.x && c.x <= rb.x && c.y >= rb.y && c.y <= lt.y)
            {
                //try to extract the class ID for that box
                int objID = 0;
                if (System.Int32.TryParse(SceneOrganizer.Instance.boxes[i].name, out objID))
                {
                    //get all values for that class
                    ProductDefinition productDefinition = SceneOrganizer.Instance.GetProductNutri(objID);

                    //change the values of the panel
                    GameObject Calories = gameObject.transform.Find("Calories").gameObject;
                    Calories.transform.Find("Value").GetComponent<TextMesh>().text = productDefinition.calories;
                    Calories.transform.Find("default").GetComponent<Renderer>().material.color = SceneOrganizer.Instance.GetNutriColor(productDefinition.caloriesColor);
                    GameObject Sugar = gameObject.transform.Find("Sugar").gameObject;
                    Sugar.transform.Find("Value").GetComponent<TextMesh>().text = productDefinition.sugar;
                    Sugar.transform.Find("default").GetComponent<Renderer>().material.color = SceneOrganizer.Instance.GetNutriColor(productDefinition.sugarColor);
                    GameObject Fat = gameObject.transform.Find("Fat").gameObject;
                    Fat.transform.Find("Value").GetComponent<TextMesh>().text = productDefinition.fat;
                    Fat.transform.Find("default").GetComponent<Renderer>().material.color = SceneOrganizer.Instance.GetNutriColor(productDefinition.fatColor);
                    GameObject Protein = gameObject.transform.Find("Protein").gameObject;
                    Protein.transform.Find("Value").GetComponent<TextMesh>().text = productDefinition.protein;
                    Protein.transform.Find("default").GetComponent<Renderer>().material.color = SceneOrganizer.Instance.GetNutriColor(productDefinition.proteinColor);
                    GameObject Fibre = gameObject.transform.Find("Fiber").gameObject;
                    Fibre.transform.Find("Value").GetComponent<TextMesh>().text = productDefinition.fiber;
                    Fibre.transform.Find("default").GetComponent<Renderer>().material.color = SceneOrganizer.Instance.GetNutriColor(productDefinition.fiberColor);
                    GameObject GTIN = gameObject.transform.Find("GTIN").gameObject;
                    GTIN.GetComponent<TextMesh>().text = productDefinition.gtin;
                    GameObject ProductName = gameObject.transform.Find("ProductName").gameObject;
                    ProductName.GetComponent<TextMesh>().text = productDefinition.name;

                    //display the panel
                    gameObject.SetActive(true);

                    break;
                }
            } 
        }
    }
}
