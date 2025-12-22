using UnityEngine;

public class DualDisplay : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("연결된 화면 수: " + Display.displays.Length);

        if(Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
        }
    }

}
