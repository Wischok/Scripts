using UnityEngine;
using TMPro;

public class DisplayBonusPoints : MonoBehaviour
{
    [SerializeField] private GameObject m_bonusPointsTextPrefab;

    //singleton
    private static DisplayBonusPoints m_instance;
    public static DisplayBonusPoints Instance { get { return m_instance; } }

    private void Awake()
    {
        m_instance = this;
    }
    
    public void ShowBonusPoints(int points, Vector3 position)
    {
        GameObject bonusTextObj = Instantiate(m_bonusPointsTextPrefab, position, Quaternion.identity, transform);
        TextMeshProUGUI bonusTextComponent = bonusTextObj.GetComponent<TextMeshProUGUI>();

        if(bonusTextComponent != null)
        {
            bonusTextComponent.text = "+" + points.ToString();
        }
    }
}
