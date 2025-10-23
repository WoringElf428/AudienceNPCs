using System.Collections.Generic;
using UnityEngine;

public class AudienceData : MonoBehaviour
{
    [SerializeField] public List<int> neighborIndex;
    [SerializeField] public int[] clip_index = new int[4];  //0>none, 1-4>clip.001~004
    public void AddNeighbor(int index)
    {
        neighborIndex.Add(index);
    }
}
