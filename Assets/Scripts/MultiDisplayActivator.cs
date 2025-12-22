using UnityEngine;

public class MultiDisplayActivator : MonoBehaviour
{
    void Start()
    {
        // Display.displays[0] = Display 1 (기본)
        // Display.displays[1] = Display 2
        // Display.displays[2] = Display 3 ...

        for (int i = 1; i < Display.displays.Length; i++)
        {
            Display.displays[i].Activate();
        }
    }
}