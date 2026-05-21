/// Description:
///     A generic monobehaviour script to hold debug variables
///     functions and specific gizmos for visualizing helpful vectors.
using System.Collections.Generic;
using UnityEngine;

public class DebugUtils : MonoBehaviour
{
    ///
    /// Inspector Variables ---------------------------------------------------------------
    /// 
    
    [SerializeField] private bool showRuler = false;//whether to show the ruler gizmo or not
    [SerializeField] private float rulerLength = 1f;//length of the ruler
    [SerializeField] private List<Transform> rulers;//position of the ruler

    Vector3 rulerStartPoint;
    private void OnDrawGizmos()
    {
        if (showRuler)
        {
            foreach (Transform rulerPosition in rulers)
            {
                //green for ruler
                Gizmos.color = Color.green;

                //check if ruler position is assigned
                if (rulerPosition == null)
                    rulerStartPoint = transform.position;
                else
                    rulerStartPoint = rulerPosition.position;

                //draw a box with the length of the ruler and a small width for visibility
                Gizmos.DrawWireCube(rulerStartPoint + new Vector3(0.5f, rulerLength / 2, 0), new Vector3(1f, rulerLength, 0));

                var subTickStyle = new GUIStyle(UnityEditor.EditorStyles.label) { fontSize = 11 };

                Gizmos.color = Color.white;
                for (int i = 0; i <= rulerLength; i++)
                {
                    // Major tick — full width with label
                    Gizmos.DrawLine(rulerStartPoint + new Vector3(0, i, 0), rulerStartPoint + new Vector3(0.5f, i, 0));
                    UnityEditor.Handles.Label(rulerStartPoint + new Vector3(-0.3f, i, 0), i.ToString());

                    // 9 sub-ticks between this step and the next
                    if (i < rulerLength)
                    {
                        for (int j = 1; j <= 9; j++)
                        {
                            float subY    = i + j * 0.1f;
                            float tickLen = j == 5 ? 0.37f : 0.25f;
                            Gizmos.DrawLine(rulerStartPoint + new Vector3(0, subY, 0),
                                            rulerStartPoint + new Vector3(tickLen, subY, 0));
                            UnityEditor.Handles.Label(rulerStartPoint + new Vector3(-0.1f, subY, 0), j.ToString(), subTickStyle);
                        }
                    }
                }

                //draw final line at the end of the ruler
                Gizmos.DrawLine(rulerStartPoint, rulerStartPoint + new Vector3(0, rulerLength, 0));
            }
        }
    }
}
