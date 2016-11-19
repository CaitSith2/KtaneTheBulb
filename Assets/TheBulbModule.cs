﻿using System;
using System.Collections;
using System.Linq;
using TheBulb;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of The Bulb
/// Created by Timwi
/// </summary>
public class TheBulbModule : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;

    public Light Light1;
    public Light Light2;
    public MeshRenderer Glass;

    public KMSelectable ButtonO;
    public KMSelectable Bulb;
    public KMSelectable ButtonI;

    public Transform OFace;
    public Transform IFace;

    enum BulbColor
    {
        Blue,
        Red,
        Green,
        Yellow,
        White,
        Purple
    }

    private Color[] _bulbColors = Ext.NewArray(
        "6AA8FF",   // blue (was 3A9DFF)
        "FF0005",   // red (was FF1E00)
        "2EFD2F",   // green
        "EAE11F",   // yellow
        "D2D2D2",   // white
        "F21DFF"    // purple
    )
        .Where(s => s != null)
        .Select(c => new Color(Convert.ToInt32(c.Substring(0, 2), 16) / 255f, Convert.ToInt32(c.Substring(2, 2), 16) / 255f, Convert.ToInt32(c.Substring(4, 2), 16) / 255f))
        .ToArray();

    private BulbColor _bulbColor;
    private bool _opaque;
    private bool _initiallyOn;
    private bool _wentOffAtStep1;
    private bool _rememberedIndicatorPresent;
    private bool _isBulbUnscrewed;
    private bool _mustUndoBulbScrewing;
    private bool _pressedOAtStep1;
    private bool _wentOnAtScrewIn;
    private int _stage;

    void Start()
    {
        if (Rnd.Range(0, 2) == 0)
        {
            // Swap the buttons! Fun fun
            var p = IFace.localPosition;
            IFace.localPosition = OFace.localPosition;
            OFace.localPosition = p;
            var t = ButtonO;
            ButtonO = ButtonI;
            ButtonI = t;
        }

        var colorIndex = Rnd.Range(0, _bulbColors.Length);
        _bulbColor = (BulbColor) colorIndex;
        _opaque = Rnd.Range(0, 2) == 0;
        _initiallyOn = Rnd.Range(0, 2) == 0;
        Glass.material.color = Light1.color = Light2.color = _bulbColors[colorIndex].WithAlpha(_opaque ? 1f : .55f);
        _stage = 0;
        _isBulbUnscrewed = false;

        Debug.LogFormat("[TheBulb] Initial state: Color={0}, Opaque={1}, Initially on={2}", _bulbColor, _opaque, _initiallyOn);

        ButtonO.OnInteract += delegate { ButtonO.AddInteractionPunch(); HandleButtonPress(o: true); return false; };
        ButtonI.OnInteract += delegate { ButtonI.AddInteractionPunch(); HandleButtonPress(o: false); return false; };
        Bulb.OnInteract += delegate { Bulb.AddInteractionPunch(); HandleBulb(); return false; };

        Module.OnActivate += delegate
        {
            TurnLights(on: _initiallyOn);
            _stage = 1;
        };
    }

    private void TurnLights(bool on)
    {
        Light1.enabled = on;
        Light2.enabled = on;
    }

    private bool _isScrewing;

    private IEnumerator Screw(bool @in)
    {
        for (int i = 0; i < 360 / 10; i++)
        {
            yield return null;
            Bulb.transform.Rotate(Vector3.up, @in ? 15 : -15);
            Bulb.transform.Translate(new Vector3(0, @in ? -.0015f : .0015f, 0));
        }
        _isScrewing = false;
        if (_wentOnAtScrewIn)
            TurnLights(on: true);
    }

    private void HandleBulb()
    {
        if (_isScrewing)
            return;
        _isScrewing = true;
        StartCoroutine(Screw(_isBulbUnscrewed));
        _isBulbUnscrewed = !_isBulbUnscrewed;

        if (_mustUndoBulbScrewing)
        {
            Debug.LogFormat("[TheBulb] Undoing incorrect {0}.", _isBulbUnscrewed ? "screwing in" : "unscrewing");
            _mustUndoBulbScrewing = false;
            return;
        }

        var isCorrect = true;
        var origStage = _stage;

        if (_stage >= 200)
        {
            _stage -= 200;
            if (_stage == 12 || _stage == 13)
                _wentOnAtScrewIn = (Rnd.Range(0, 2) == 0);
        }
        else if (_stage >= 100)
            _stage -= 100;
        else
        {
            isCorrect = false;

            switch (_stage)
            {
                case 1:
                    if (isCorrect = !_initiallyOn)
                        _stage = 4;
                    break;

                case 2:
                    if (isCorrect = (_bulbColor != BulbColor.Red && _bulbColor != BulbColor.White))
                        _stage = 7;
                    break;

                case 3:
                    if (isCorrect = (_bulbColor != BulbColor.Green && _bulbColor != BulbColor.Purple))
                        _stage = 8;
                    break;

                case 9:
                    isCorrect = (_bulbColor == BulbColor.Purple || _bulbColor == BulbColor.Red) && !_isBulbUnscrewed;
                    break;

                case 10:
                    isCorrect = (_bulbColor == BulbColor.Green || _bulbColor == BulbColor.White) && !_isBulbUnscrewed;
                    break;
            }
        }

        Debug.LogFormat("[TheBulb] {0} at stage {1}: {2}.", _isBulbUnscrewed ? "Unscrewing" : "Screwing in", origStage, isCorrect ? "CORRECT, stage is now: " + _stage : "WRONG");
        if (isCorrect && _stage == 0)
            Module.HandlePass();
        else if (!isCorrect)
        {
            Module.HandleStrike();
            _mustUndoBulbScrewing = true;
        }
    }

    private void HandleButtonPress(bool o)
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Bulb.transform);

        if (_mustUndoBulbScrewing)
        {
            Debug.LogFormat("[TheBulb] The light bulb should have been {0} before pressing any more buttons.", _isBulbUnscrewed ? "screwed back in" : "unscrewed again");
            Module.HandleStrike();
            return;
        }

        var isCorrect = false;
        var origStage = _stage;

        switch (_stage)
        {
            case 1:
                if (_initiallyOn && (_opaque ? o : !o))
                {
                    isCorrect = true;
                    if (_opaque
                        ? (_bulbColor == BulbColor.Green || _bulbColor == BulbColor.Purple)
                        : (_bulbColor == BulbColor.Red || _bulbColor == BulbColor.White))
                        TurnLights(on: !(_wentOffAtStep1 = (Rnd.Range(0, 2) == 0)));
                    else
                        TurnLights(on: false);
                    _stage = o ? 3 : 2;
                    _pressedOAtStep1 = o;
                }
                break;

            case 2:
                if ((_bulbColor == BulbColor.Red && !o) || (_bulbColor == BulbColor.White && o))
                {
                    isCorrect = true;
                    TurnLights(on: false);
                    _stage = o ? 106 : 105;
                }
                break;

            case 3:
                if ((_bulbColor == BulbColor.Green && !o) || (_bulbColor == BulbColor.Purple && o))
                {
                    isCorrect = true;
                    TurnLights(on: false);
                    _stage = o ? 105 : 106;
                }
                break;

            case 4:
                if (isCorrect = (Bomb.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.CAR) ||
                    Bomb.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.IND) ||
                    Bomb.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.MSA) ||
                    Bomb.IsIndicatorPresent(KMBombInfoExtensions.KnownIndicatorLabel.SND) ? !o : o))
                    _stage = o ? 10 : 9;
                break;

            case 5:
                if (isCorrect = (_wentOffAtStep1 ? (_pressedOAtStep1 == o) : (_pressedOAtStep1 != o)))
                    _stage = 200;
                break;

            case 6:
                if (isCorrect = ((_wentOffAtStep1 ^ _pressedOAtStep1) ? (_pressedOAtStep1 == o) : (_pressedOAtStep1 != o)))
                    _stage = 200;
                break;

            case 7:
                _rememberedIndicatorPresent = Bomb.IsIndicatorPresent(_bulbColor == BulbColor.Blue ? KMBombInfoExtensions.KnownIndicatorLabel.CLR : KMBombInfoExtensions.KnownIndicatorLabel.SIG);
                if (isCorrect = ((_bulbColor == BulbColor.Green) || (_bulbColor == BulbColor.Purple) ? !o : o))
                    _stage = (_bulbColor == BulbColor.Blue) || (_bulbColor == BulbColor.Green) ? 11 : (_bulbColor == BulbColor.Purple) ? 212 : 213;
                break;

            case 8:
                _rememberedIndicatorPresent = Bomb.IsIndicatorPresent(_bulbColor == BulbColor.White ? KMBombInfoExtensions.KnownIndicatorLabel.FRQ : KMBombInfoExtensions.KnownIndicatorLabel.FRK);
                if (isCorrect = ((_bulbColor == BulbColor.White) || (_bulbColor == BulbColor.Red) ? !o : o))
                    _stage = (_bulbColor == BulbColor.White) || (_bulbColor == BulbColor.Yellow) ? 11 : (_bulbColor == BulbColor.Red) ? 213 : 212;
                break;

            case 9:
                if (isCorrect = (
                    _bulbColor == BulbColor.Blue || _bulbColor == BulbColor.Green ? !o :
                    _bulbColor == BulbColor.Yellow || _bulbColor == BulbColor.White ? o :
                    _bulbColor == BulbColor.Purple ? !_isBulbUnscrewed && !o : !_isBulbUnscrewed && o))
                    _stage =
                        _bulbColor == BulbColor.Blue ? 14 :
                        _bulbColor == BulbColor.Green ? 212 :
                        _bulbColor == BulbColor.Yellow ? 15 :
                        _bulbColor == BulbColor.White ? 213 :
                        _bulbColor == BulbColor.Purple ? 12 : 13;
                break;

            case 10:
                if (isCorrect = (
                    _bulbColor == BulbColor.Purple || _bulbColor == BulbColor.Red ? !o :
                    _bulbColor == BulbColor.Blue || _bulbColor == BulbColor.Yellow ? o :
                    _bulbColor == BulbColor.Green ? !_isBulbUnscrewed && !o : !_isBulbUnscrewed && o))
                    _stage =
                        _bulbColor == BulbColor.Purple ? 14 :
                        _bulbColor == BulbColor.Red ? 213 :
                        _bulbColor == BulbColor.Blue ? 15 :
                        _bulbColor == BulbColor.Yellow ? 212 :
                        _bulbColor == BulbColor.Green ? 13 : 12;
                break;

            case 11:
                if (isCorrect = (_rememberedIndicatorPresent ? !o : o))
                    _stage = 200;
                break;

            case 12:
                if (isCorrect = (_wentOnAtScrewIn ? !o : o))
                    _stage = 0;
                break;

            case 13:
                if (isCorrect = (_wentOnAtScrewIn ? o : !o))
                    _stage = 0;
                break;

            case 14:
                if (isCorrect = (_opaque ? !o : o))
                    _stage = 200;
                break;

            case 15:
                if (isCorrect = (_opaque ? o : !o))
                    _stage = 200;
                break;
        }

        Debug.LogFormat("[TheBulb] Pressing {0} at stage {1} with the bulb {2}: {3}.", o ? "O" : "I", origStage, _isBulbUnscrewed ? "unscrewed" : "screwed in", isCorrect ? "CORRECT, stage is now: " + _stage : "WRONG");
        if (!isCorrect)
            Module.HandleStrike();
        else if (_stage == 0)
        {
            TurnLights(on: false);
            Module.HandlePass();
        }
    }
}
