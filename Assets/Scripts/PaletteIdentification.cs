using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using System.Linq;

public sealed class PaletteIdentification : MonoBehaviour
{
    [SerializeField]
    KMBombModule _solvable;

    [SerializeField]
    KMAudio _audio;

    [SerializeField]
#if UNITY_EDITOR
    public
#endif
    Pixel[] _buttons;

    [SerializeField]
    GameObject _decalGood, _decalBad;

    static int s_lastModuleId;

    int _moduleId;

    int[][] _bitmaps;
    int _chosenBitmapIx, _highlightedPixel = -1;
    readonly List<int> _selectedPixels = new List<int>(4), _usedHighlights = new List<int>(3);
    int[] _palette;
    int[] ChosenBitmap => _bitmaps[_chosenBitmapIx];
    Color[] _colors;

    void Log(string message, params object[] args)
    {
        var log = string.Format(message, args);
        Debug.LogFormat("[{0} #{1}] {2}", _solvable.ModuleDisplayName, _moduleId, log);
    }

    void Start()
    {
        _moduleId = ++s_lastModuleId;

        #region Ruleseed
        var rs = GetComponent<KMRuleSeedable>().GetRNG();
        Log("Using ruleseed {0}.", rs.Seed);
        var alpha = Enumerable.Repeat(Enumerable.Range(0, 16), 4).SelectMany(x => x).ToArray();
        _bitmaps = RepeatCall(() => alpha.OrderBy(_ => rs.NextDouble()).ToArray(), 16).ToArray();
        #endregion

        #region Puzzle Generation
        _chosenBitmapIx = Random.Range(0, 16);
        int iter = 0;
        do
        {
            _palette = RepeatCall(() => Random.Range(0, 4), 16).ToArray();
            if(++iter == 1600)
            {
                Log("This ruleseed appears to have no solutions. Please contact Bagels so this can be resolved.");
                _solvable.HandlePass();
                throw new Exception();
            }
            else if(iter % 100 == 0)
            {
                _chosenBitmapIx++;
                _chosenBitmapIx %= 16;
            }
        }
        while (!new int[] { 0, 1, 2, 3 }.All(_palette.Contains) || !Unique(_chosenBitmapIx, _palette));
        float hue = Random.Range(0f, 1f);
        var baseColors = Enumerable.Range(0, 4)
            .Select(i => Color.HSVToRGB((hue + 0.25f * i) % 1f, Random.Range(0.5f, 1f), Random.Range(0.7f, 1f)))
            .ToArray();
        Func<Color, float> value = c =>
        {
            float h, x;
            Color.RGBToHSV(c, out x, out x, out h);
            return h;
        };
        _colors = baseColors
            .Concat(
                Enumerable.Range(0, 4)
                    .Select(i => Color.HSVToRGB((hue + 0.5f + 0.25f * i) % 1f, Random.Range(.3f, .8f), Random.Range(value(baseColors[i]) - 0.4f, value(baseColors[i]) - 0.2f)))
            ).ToArray();

        Log("Using bitmap {0}. (Starting row {1})", _chosenBitmapIx + 1, ChosenBitmap.Take(8).Select(i => Convert.ToString(i, 16).ToUpperInvariant()).Join(""));
        Log("Palette assignments: {0}", Enumerable.Range(0, 16).Select(i => "(" + Convert.ToString(i, 16).ToUpperInvariant() + ": " + _palette[i] + ")").Join(" "));
        Log("Palette colors: {0}", Enumerable.Range(0, 4).Select(i => "(" + i + ": " + _colors[i] + ")").Join(" "));
        Log("Displayed grid: {0}", Chunk(Enumerable.Range(0, 64).Select(i => _palette[ChosenBitmap[i]]).Join(""), 8).Join("/"));
        #endregion

        for (int i = 0; i < _buttons.Length; i++)
        {
            int j = i;
            _buttons[i].OnInteract += Press(j);
            _buttons[i].SetDecals();
            _buttons[i].SetColor(_colors[_palette[ChosenBitmap[i]]], _colors[_palette[ChosenBitmap[i]] + 4]);
        }

        _solvable.OnActivate += Generate;

        SetColorblind(_colorblindActive = GetComponent<KMColorblindMode>().ColorblindModeActive);
        _decalGood.SetActive(false);
        _decalBad.SetActive(false);
    }

    IEnumerable<string> Chunk(string s, int len)
    {
        int i = 0;
        while (i < s.Length)
        {
            yield return s.Substring(i, len);
            i += len;
        }
    }

    Func<bool> Press(int button)
    {
        Pixel px = _buttons[button];
        return () =>
        {
            _audio.PlaySoundAtTransform("press", px.transform);

            if (_highlightedPixel == -1 || _selectedPixels.Contains(button))
            {
                px.InteractionPunch();
                return _selectedPixels.Contains(button);
            }

            if (ChosenBitmap[button] == ChosenBitmap[_highlightedPixel])
            {
                Log("You correctly selected pixel {0}.", button + 1);
                px.InteractionPunch();
                _selectedPixels.Add(button);
                px.SetDot(true);
                return Check();
            }

            Log("You incorrectly selected pixel {0}. Strike!", button + 1);
            px.InteractionPunch(1f);
            Strike();

            return false;
        };
    }

    bool Check()
    {
        if (_selectedPixels.Count < 4)
            return true;
        _usedHighlights.Add(_highlightedPixel);
        Log("That's {0} stage{1} done.", _usedHighlights.Count, _usedHighlights.Count == 1 ? "" : "s");
        if (_usedHighlights.Count == 4)
        {
            Log("Good job! That's a solve.");
            _audio.PlaySoundAtTransform("solve", transform);
            _solvable.HandlePass();
            _highlightedPixel = -1;
            foreach (var px in _buttons)
                px.SetDecals();
            _selectedPixels.Clear();
            _decalGood.SetActive(true);
            return false;
        }
        Generate();
        return false;
    }

    void Strike()
    {
        _solvable.HandleStrike();
        StartCoroutine(Flash());
        Generate();
    }

    IEnumerator Flash()
    {
        _decalBad.SetActive(true);
        yield return new WaitForSeconds(1f);
        _decalBad.SetActive(false);
    }

    void Generate()
    {
        _selectedPixels.Clear();
        var forbidden = _usedHighlights.Concat(new int[] { _highlightedPixel }).ToArray();
        do _highlightedPixel = Random.Range(0, 64);
        while (forbidden.Contains(_highlightedPixel));

        foreach (var px in _buttons)
            px.SetDecals();

        _buttons[_highlightedPixel].SetBorder(true);

        Log("Pixel {0} is now highlighted. (Raw form {1})", _highlightedPixel + 1, Convert.ToString(ChosenBitmap[_highlightedPixel], 16).ToUpperInvariant());
    }

    bool Unique(int bitmap, int[] palette)
    {
        var assignment = Assign(_bitmaps[bitmap], palette);
        return Enumerable.Range(0, 16).Except(new int[] { bitmap })
             .All(bmp => !Assign(_bitmaps[bmp], palette).SequenceEqual(assignment));
    }

    IEnumerable<int> Assign(int[] bmp, int[] palette) => bmp.Select(i => palette[i]);

    IEnumerable<T> RepeatCall<T>(Func<T> selector, int count) => Enumerable.Repeat(0, count).Select(_ => selector());

    void SetColorblind(bool on)
    {
        if (on)
            for (int i = 0; i < 64; i++)
                _buttons[i].SetColorblind(_palette[ChosenBitmap[i]].ToString());
        else
            foreach (var px in _buttons)
                px.SetColorblind("");
    }

    string TwitchHelpMessage;
    bool _colorblindActive;

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToLowerInvariant();
        if (command == "cb" || command == "colorblind")
        {
            SetColorblind(_colorblindActive ^= true);
            yield return null;
            yield break;
        }
        throw new NotImplementedException();
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        throw new NotImplementedException();
    }
}
