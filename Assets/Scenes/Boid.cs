using Gist2.Extensions.ComponentExt;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class Boid {

    protected Random rand;
    protected Tuner currTuner = new Tuner();

    public Boid() {
        rand = Random.CreateFromIndex(31);
    }

    #region interface
    public Tuner CurrTuner {
        get => currTuner.DeepCopy();
        set {
            var copy = value.DeepCopy();
            currTuner = Validate(copy);
        }
    }

    public float2 RandomOnCircle() {
        var r = rand.NextFloat() * CIRCLE_RAD;
        return new float2(math.cos(r), math.sin(r));
    }
    public float3 GetWanderForce(WanderData data, ICoordinates coord) {
        var forward = coord.Forward;
        var right = coord.Right;
        var dt = Time.deltaTime;
        var wander = CurrTuner.wander;

        var wt_local = data.wanderTarget;
        wt_local += dt * wander.jitter * RandomOnCircle();
        wt_local = math.normalizesafe(wt_local);
        wt_local *= wander.radius;
        data.wanderTarget = wt_local;

        var target = wt_local.x * right + (wt_local.y + wander.distance) * forward;
        return target;
    }
    #endregion

    #region methods
    private Tuner Validate(Tuner copy) {
        var wander = copy.wander;
        wander.distance = math.max(wander.distance, 1e-2f);
        wander.radius = math.clamp(wander.radius, 0f, wander.distance - 1e-2f);
        return copy;
    }
    #endregion

    #region declarations
    public const float CIRCLE_RAD = 2f * math.PI;

    public interface ICoordinates {
        float3 Position { get; }
        float3 Forward { get; }
        float3 Right { get; }
        float3 Upward { get; }
    }
    public class WanderData {
        public float2 wanderTarget = new float2(0f, 1f);
    }

    [System.Serializable]
    public class WanderTuner {
        public float jitter = 10f;
        public float radius = 0.8f;
        public float distance = 1.2f;
    }
    [System.Serializable]
    public class Tuner {
        public WanderTuner wander = new WanderTuner();
    }
    #endregion
}