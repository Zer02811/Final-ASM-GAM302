using Fusion;
using TMPro;
using UnityEngine;

public class UILogin : MonoBehaviour
{
    public TMP_InputField usernameInput;
    public FusionBootstrap fusionBootstrap;

    public void Login()
    {
        string username = usernameInput.text;
        PlayerPrefs.SetString("username", username);
        fusionBootstrap.StartSharedClient();
    }
}
