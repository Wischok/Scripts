using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class PointPopUp : MonoBehaviour
{
    [SerializeField] private float m_timer = 2.0f;
    private float m_timeElapsed = 0f;
    private TextMeshProUGUI m_textMesh;

    private void Awake()
    {
        m_textMesh = GetComponent<TextMeshProUGUI>();
    }

    private void Update()
    {
        if(m_timeElapsed >= m_timer)
        {
            Destroy(gameObject);
        }
        else
        {
            m_timeElapsed += Time.deltaTime;
            //move up over time
            transform.position += new Vector3(0f, 1f * Time.deltaTime, 0f);

            //fade out over time
            float alpha = Mathf.Lerp(1f, 0f, m_timeElapsed / m_timer);
            Color currentColor = m_textMesh.color;
            currentColor.a = alpha;
            m_textMesh.color = currentColor;
        }
    }

    //destroy game object after animation is done
    public void DestroyAfterAnimation()
    {
        Destroy(gameObject);
    }
}
