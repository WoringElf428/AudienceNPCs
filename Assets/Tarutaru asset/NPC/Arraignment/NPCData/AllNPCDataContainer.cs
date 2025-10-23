using System; // Required for [Serializable]
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// This struct will hold the data for a single audience member
[Serializable] // Make it serializable so Unity can save it within a ScriptableObject
public struct NPCStructData
{
    public List<int> neighborIndex;
    public List<float> neighborDist;
    public int[] clip_index;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
    public bool isParticle;
}

// This ScriptableObject will hold an array of AudienceStructData
[CreateAssetMenu(
    fileName = "AllAudienceData",
    menuName = "Audience/All Audience Data Container",
    order = 1
)]
public class AllNPCDataContainer : ScriptableObject
{
    public List<NPCStructData> audienceMembers;
    public int maxNeighbors;
}