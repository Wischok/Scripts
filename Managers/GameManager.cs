using UnityEngine;

public class GameManager : MonoBehaviour
{
    //singletone
    private static GameManager m_instance;
    public static GameManager Instance => m_instance;

    private void Awake()
    {
        //initialize singleton if empty
        if(m_instance == null)
        {
            m_instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    private void Start()
    {
        //override camera sort mode
        Camera.main.transparencySortMode = TransparencySortMode.CustomAxis;
        Camera.main.transparencySortAxis = new Vector3(0,1,0);
    }

    public void TogglePause()
    {
        if(Time.timeScale == 1f)
        {
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = 1f;
        }
    }
}
