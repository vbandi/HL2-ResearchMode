using System;
using TMPro;
using UnityEngine;

public class Ruler : MonoBehaviour
{
    public Vector3 From;
    public Vector3 To;

    private LineRenderer _line;
    public TextMeshPro Label;

    private void Start()
    {
        _line = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        var distance = Vector3.Distance(From, To);

        if (distance < 0.01)
        {
            _line.enabled = false;
            Label.enabled = false;
            return;
        }

        _line.enabled = true;
        Label.enabled = true;
        
        _line.SetPositions(new []{From, To});

        Label.transform.position = From + (Vector3.up * 0.05f);
        Label.text = (distance * 100).ToString("F1");
    }
}
