using UnityEngine;

public class ControlsController : MonoBehaviour
{
    public UIManager nav;
    public void Back()
    {
        Debug.Log("Back");
        nav.Back();
    }
}
