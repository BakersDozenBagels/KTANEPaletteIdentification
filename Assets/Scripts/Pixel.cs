using System;
using UnityEngine;
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
#endif

[Serializable]
public sealed class Pixel : MonoBehaviour
{
#if UNITY_EDITOR
    [UnityEditor.MenuItem("Pixels/Layout")]
    static void Layout()
    {
        List<Pixel> pxs = new List<Pixel>(64) { FindObjectOfType<Pixel>() };
        var template = pxs[0].gameObject;
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                if (x + y == 0)
                    continue;
                var n = Instantiate(template);
                n.transform.parent = template.transform.parent;
                n.transform.localPosition = new Vector3(0.0125f * y, template.transform.localPosition.y, -0.0125f * x);
                n.transform.localScale = template.transform.localScale;
                pxs.Add(n.GetComponent<Pixel>());
            }

        FindObjectOfType<PaletteIdentification>()._buttons = pxs.ToArray();
        pxs[0]._button.Parent.Children = pxs.Select(p => p._button).ToArray();
    }
#endif

    [SerializeField]
    KMSelectable _button;
    [SerializeField]
    Renderer _color, _border, _dot, _hl;
    [SerializeField]
    TextMesh _colorblind;

    public event Func<bool> OnInteract = () => true;

    public void SetColor(Color c, Color i)
    {
        _color.material.color = c;
        _border.material.color = i;
        _dot.material.color = i;
        _hl.material.color = i;
        _colorblind.color = i;
    }

    public void SetDot(bool on) => _dot.gameObject.SetActive(on);
    public void SetBorder(bool on) => _border.gameObject.SetActive(on);
    public void SetDecals(bool dot, bool border)
    {
        SetDot(dot);
        SetBorder(border);
    }
    public void SetDecals() => SetDecals(false, false);
    public void InteractionPunch(float f = .2f) => _button.AddInteractionPunch(f);

    public void SetColorblind(string s) => _colorblind.text = s;

    void Awake() => SetColorblind("");

    void Start()
    {
        _button.OnInteract += Press;
        _button.OnHighlight += () => { _hl.gameObject.SetActive(true); };
        _button.OnHighlightEnded += () => { _hl.gameObject.SetActive(false); };
        _hl.gameObject.SetActive(false);
    }

    private bool Press()
    {
        SetDot(OnInteract());
        return false;
    }
}