using UnityEngine;

public class LoadingIcon : MonoBehaviour {

    public static LoadingIcon Instance;

	// Use this for initialization
	void Awake () {
        Instance = this;

        gameObject.SetActive(true);
    }

}
