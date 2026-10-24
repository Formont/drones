using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonBehaviour: MonoBehaviour
{
    public void SettingsClick()
    {
        SceneManager.LoadScene("Settings");
    }

    public void CampaignClick()
    {
        SceneManager.LoadScene("CampaignMap");
    }

    public void MenuClick()
    {
        SceneManager.LoadScene(0);
    }
}
