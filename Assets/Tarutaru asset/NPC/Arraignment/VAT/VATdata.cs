using System.Collections.Generic;
using UnityEngine;
public struct VATmotion
{
    public float start_pos;
    public float end_pos;
    public int length;
    public float cycle_scale;
}

// This ScriptableObject will hold an array of AudienceStructData
[CreateAssetMenu(
    fileName = "AllAudienceData",
    menuName = "Audience/All Audience Data Container",
    order = 1
)]
public class VATdata : ScriptableObject
{
    public List<VATmotion> decordData;
    public float mergin;
}