using UnityEngine;
using UnityEngine.UI;

public class GenderPlayerController : MonoBehaviour
{
    public Button male;
    public Button female;

    public void ChooseMale()
    {
        MultiPlayerManager.Instance.LocalPlayerGender = PlayerGender.Male;
        saveMAnager.Instance.SetGender("male");
        Debug.Log("Pilih Male");
        
    }

    public void ChooseFemale()
    {
        MultiPlayerManager.Instance.LocalPlayerGender = PlayerGender.Female;
        saveMAnager.Instance.SetGender("female");
        Debug.Log("Pilih Female");
    }
}
