using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CameraFollow : MonoBehaviour
{
    public Transform[] target;
    public Vector3 offset;
    public int index=0;
    private bool initButtons=false;


    void LateUpdate()
    {
        if (initButtons != false)
        {
            Debug.Log("forward button"+GameObject.FindGameObjectWithTag("HomeForwardDogButton"));
            Button forwardButton = GameObject.FindGameObjectWithTag("HomeForwardDogButton").GetComponent<Button>();
            Button backButton = GameObject.FindGameObjectWithTag("HomeBackDogButton").GetComponent<Button>();
            forwardButton.onClick.AddListener(changeIndexForward);
            backButton.onClick.AddListener(changeIndexBack);
            initButtons = false;
        }
        if (target != null)
        {
            //transform.position = target.position + offset; // Adjust camera position
            transform.LookAt(target[index]);                     // Make camera face the object
        }
    }

    void Start()
    {
        Button skipIntroButton = GameObject.FindGameObjectWithTag("SkipLoadingTag").GetComponent<Button>();
        skipIntroButton.onClick.AddListener(changeScene);
    }

    private void changeIndexForward()
    {
        int arrayLength = target.Length;
        //index = (index + 1) % arrayLength;
        if ((index + 1) > arrayLength - 1)
        {
            index = 0;
        }
        else
        {
            index += 1;
        }
    }

    private void changeIndexBack()
    {
        int arrayLength = target.Length;
        //index = (index - 1) % arrayLength;
        if ((index - 1) < 0 )
        {
            index = arrayLength - 1;
        }
        else
        {
            index -= 1;
        }
    }

    private void changeScene()
    {
        initButtons = true;
        Debug.Log("clicked on skip!");
    }
}
