using UnityEngine;

/// <summary>Editable content for a mission marker on the campaign map.</summary>
public sealed class CampaignMissionDefinition : MonoBehaviour
{
    [Header("Карточка миссии")]
    [SerializeField] private string missionTitle = "Школа оператора дрона";
    [TextArea(3, 7)]
    [SerializeField] private string description = "Ваш первый самостоятельный вылет. Пройдите шесть учебных колец по очереди, удерживая дрон в воздухе и обходя здания и машины. Любое столкновение завершит тренировку. После успешного полёта на карте откроются новые задания.";
    [SerializeField] private string estimatedTime = "Около 4 минут";

    [Header("Подпись на карте")]
    [SerializeField] private string operationCode = "T-001";
    [SerializeField] private string markerLabel = "УЧЁБА";
    [SerializeField] private string sceneName = "lvl 1";

    public string MissionTitle => missionTitle;
    public string Description => description;
    public string EstimatedTime => estimatedTime;
    public string OperationCode => operationCode;
    public string MarkerLabel => markerLabel;
    public string SceneName => sceneName;

    public void SetMissionContent(string title, string missionDescription, string time, string code, string label, string scene = "lvl 1")
    {
        missionTitle = title;
        description = missionDescription;
        estimatedTime = time;
        operationCode = code;
        markerLabel = label;
        sceneName = scene;
    }
}
