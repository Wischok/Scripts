using UnityEngine;

public class RotateTowardsGameObject : MonoBehaviour
{
    public Transform m_target;
    public Transform m_anchor;

    //distance from anchor to target
    [SerializeField] private float m_radius = 1f;

    // Update is called once per frame
    void Update()
    {
        //if target is not defined, ignore
        if(m_target == null || m_anchor == null) return;

        //direction towards target from anchor point
        Vector2 direction = (m_target.transform.position - m_anchor.position).normalized;

        //determine angle to ball
        float theta = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 270;

        if(theta < 0)
            theta += 360f;

        //rotate towards target
        transform.rotation = Quaternion.Euler(new Vector3(0, 0, theta));
        transform.localPosition = new Vector3(direction.x * m_radius, direction.y * m_radius, 0); 
    }

    public void Initialize(Transform anchor, Transform target)
    {
        //save anchor point
        m_target = target;

        //save target
        m_anchor = anchor;
    }
}
