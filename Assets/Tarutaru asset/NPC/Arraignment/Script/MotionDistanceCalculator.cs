using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

/// <summary>
/// Onuma et al. EG2008 Section 3.3: FMDistance part-wise implementation.
/// Computes per-part kinetic energy features and returns distances [total, upper, lower, limbs, trunk].
/// </summary>
public static class MotionDistanceCalculator
{
    private static readonly HumanBodyBones[] trunkBones =
    {
        HumanBodyBones.Hips,
        HumanBodyBones.Spine,
        HumanBodyBones.Chest,
        HumanBodyBones.UpperChest,
        HumanBodyBones.Neck,
        HumanBodyBones.Head
    };

    private static readonly HumanBodyBones[] armBones =
    {
        HumanBodyBones.LeftShoulder, HumanBodyBones.LeftUpperArm,
        HumanBodyBones.LeftLowerArm,    HumanBodyBones.LeftHand,
        HumanBodyBones.RightShoulder, HumanBodyBones.RightUpperArm,
        HumanBodyBones.RightLowerArm,   HumanBodyBones.RightHand
    };

    private static readonly HumanBodyBones[] legBones =
    {
        HumanBodyBones.LeftUpperLeg,  HumanBodyBones.LeftLowerLeg,
        HumanBodyBones.LeftFoot,      HumanBodyBones.LeftToes,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
        HumanBodyBones.RightFoot,     HumanBodyBones.RightToes
    };

    /// <summary>
    /// Calculates part-wise FMDistance between two AnimationClips.
    /// Returns distances in order: [total, upperBody, lowerBody, limbs, trunk].
    /// </summary>
    public static float[] CalculateFMPartDistances(
        GameObject rigPrefab,
        AnimationClip clipA,
        AnimationClip clipB,
        int sampleFrameCount = 120)
    {
        float[] aFeat = SamplePartLogEnergies(rigPrefab, clipA, sampleFrameCount);
        float[] bFeat = SamplePartLogEnergies(rigPrefab, clipB, sampleFrameCount);
        int len = Mathf.Min(aFeat.Length, bFeat.Length);
        var distances = new float[len];
        for (int i = 0; i < len; i++)
            distances[i] = Mathf.Abs(aFeat[i] - bFeat[i]);
        return distances;
    }

    /// <summary>
    /// Samples a clip and returns log-transformed energy sums per part.
    /// Output order: [e_total, e_upper, e_lower].
    /// </summary>
    public static float[] SamplePartLogEnergies(
        GameObject rigPrefab,
        AnimationClip clip,
        int sampleFrameCount)
    {
        // Instantiate rig and ensure humanoid avatar
        var rig = Object.Instantiate(rigPrefab);
        rig.hideFlags = HideFlags.HideAndDontSave;
        var animator = rig.GetComponent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            Debug.LogError("Rig prefab requires a Humanoid Animator with a valid Avatar.");
            Object.DestroyImmediate(rig);
            return new float[5];
        }

        // Gather bone transforms
        var boneMap = new System.Collections.Generic.Dictionary<HumanBodyBones, Transform>();
        foreach (var b in trunkBones)
        {
            var t = animator.GetBoneTransform(b);
            if (t != null) boneMap[b] = t;
        }
        foreach (var b in armBones)
        {
            var t = animator.GetBoneTransform(b);
            if (t != null) boneMap[b] = t;
        }
        foreach (var b in legBones)
        {
            var t = animator.GetBoneTransform(b);
            if (t != null) boneMap[b] = t;
        }
        // Gather transforms in order: trunk, arms, legs
        int tCount = trunkBones.Length;
        int aCount = armBones.Length;
        int lCount = legBones.Length;
        int totalCount = tCount + aCount + lCount;

        var transforms = new Transform[totalCount];
        int idx = 0;
        foreach (var b in trunkBones) transforms[idx++] = animator.GetBoneTransform(b);
        foreach (var b in armBones)   transforms[idx++] = animator.GetBoneTransform(b);
        foreach (var b in legBones)   transforms[idx++] = animator.GetBoneTransform(b);

        // Initialize arrays
        var prevRots = new Quaternion[totalCount];
        var energySum = new float[totalCount];

        int samples = Mathf.Max(1, sampleFrameCount);
        float dt = clip.length / samples;

        // Initial sample
        clip.SampleAnimation(rig, 0f);
        for (int i = 0; i < totalCount; i++)
            prevRots[i] = transforms[i].rotation;

        // Sample frames
        for (int s = 1; s <= samples; s++)
        {
            float t = Mathf.Min(s * dt, clip.length);
            clip.SampleAnimation(rig, t);
            for (int i = 0; i < totalCount; i++)
            {
                Quaternion cur = transforms[i].rotation;
                // delta rotation: cur * inv(prev)
                Quaternion delta = cur * Quaternion.Inverse(prevRots[i]);
                // angle in radians: 2*acos(w)
                float angle = 2f * Mathf.Acos(Mathf.Clamp(delta.w, -1f, 1f));
                float vel = angle / dt;
                energySum[i] += vel * vel;
                prevRots[i] = cur;
            }
        }

        Object.DestroyImmediate(rig);

        // Aggregate per-part mean energy
        float eTrunk = 0f, eArm = 0f, eLeg = 0f;
        idx = 0;
        for (int i = 0; i < tCount; i++) eTrunk += energySum[idx++];
        for (int i = 0; i < aCount; i++) eArm   += energySum[idx++];
        for (int i = 0; i < lCount; i++) eLeg   += energySum[idx++];

        // Compute per-part energy
        eTrunk /= samples;
        eArm   /= samples;
        eLeg   /= samples;
        float eLimbs = eArm + eLeg;
        float eTotal = eTrunk + eLimbs;
        float eUpper = eTrunk + eArm;
        float eLower = eLeg;
        
        //Debug.Log($"eArm: {eArm}");

        // Log-transform and return
        return new float[] {
            Mathf.Log(eTotal + 1f),
            Mathf.Log(eUpper + 1f),
            Mathf.Log(eLower + 1f),
        };
    }

    private static float SumBones(
        System.Collections.Generic.Dictionary<HumanBodyBones, float> energyMap,
        HumanBodyBones[] group)
    {
        float sum = 0f;
        foreach (var b in group)
            if (energyMap.TryGetValue(b, out float val)) sum += val;
        return sum;
    }
}
