using System.Collections.Generic;
using UnityEngine;

public class mipmaptestscript : MonoBehaviour
{
    public List<Color> colors = new ();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var sr = GetComponent<SpriteRenderer>();

        Texture2D t = sr.sprite.texture;

        Color[] allPixels = t.GetPixels();

        Debug.Log("colors:\n");

        foreach(Color c in allPixels)
        {
            if(colors.Contains(c))
                continue;

            if(c.a == 0)
                continue;

            colors.Add(c);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
