using UnityEngine;
using System.Collections;
using TMPro;

public class NewText: MonoBehaviour {

    Camera activeCamera;
    public bool clicked = false;
    private TextMeshPro textObj;

    // Use this for initialization
    void Start()
    {
        activeCamera = Camera.main;
        textObj = GetComponent<TextMeshPro>();
    }

    // Update is called once per frame
    void LateUpdate()
    {
        transform.LookAt(activeCamera.transform);
        transform.rotation = Quaternion.LookRotation(activeCamera.transform.forward);
    }
    public void Update()
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            var touchPos = Input.GetTouch(0).position;
            Debug.Log($"Touch at {touchPos}");
            var ray = activeCamera.ScreenPointToRay(touchPos);
            Debug.Log($"Ray casted: {ray}");
            if (Physics.Raycast(new Vector3(touchPos.x, touchPos.y, 0), Vector3.forward)) {
                Debug.Log("There was a hit");
            }
        }
    }
}
