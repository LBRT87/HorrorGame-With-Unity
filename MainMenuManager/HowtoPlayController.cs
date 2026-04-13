using UnityEngine;

public class HowtoPlayController : MonoBehaviour
{
    public UIManager nav;
    public void Back() {
        Debug.Log("Balik");
        nav.Back();
    }
}
