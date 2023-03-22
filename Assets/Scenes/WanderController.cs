using Gist2.Deferred;
using LLGraphicsUnity;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class WanderController : MonoBehaviour {

    public Links links = new Links();
    public Tuner tuner = new Tuner();

    protected Random rand;
    protected Boid wander;
    protected Validator changed = new Validator();

    protected GLMaterial gl;
    protected List<Quad> fields = new List<Quad>();
    protected List<CharacterInfo> chars = new List<CharacterInfo>();
    protected List<DataForDebug> forDebugData = new List<DataForDebug>();

    #region unity
    protected void OnEnable() {
        rand = Random.CreateFromIndex((uint)GetInstanceID());
        gl = new GLMaterial();
        wander = new Boid();

        changed.Reset();
        changed.OnValidate += () => {
            wander.CurrTuner = tuner.boid;
        };

        var fields_tr = links.fields.GetComponentsInChildren<Transform>().Where(v => v.parent == links.fields).ToArray();
        fields.AddRange(fields_tr.Select(v => v.From()));

        var n = tuner.n;
        for (var i = 0; i < n; i++) {
            var ch = Instantiate(links.fab);
            ch.gameObject.hideFlags = HideFlags.DontSave;

            var posLocal = rand.NextFloat2(-0.5f, 0.5f);
            var posWorld = fields_tr[0].TransformPoint(new float3(posLocal, 0f));
            var rotLocal = quaternion.Euler(0, 0f, rand.NextFloat(0f, 2f * math.PI));

            ch.SetParent(transform, true);
            ch.position = posWorld;
            ch.localRotation = rotLocal;

            chars.Add(new CharacterInfo() {
                tr = ch,
                coord = new TrCoordinates(ch.transform),
                wanderData = new Boid.WanderData(),
            });
        }
    }
    protected void OnDisable() {
        System.Action<Object> destructor = (Application.isPlaying ? Object.Destroy : Object.DestroyImmediate);
        foreach (var ch in chars) {
            destructor(ch.tr);
        }
        chars.Clear();

        if (gl != null) {
            gl.Dispose();
            gl = null;
        }
        fields.Clear();
    }
    private void OnValidate() {
        changed.Invalidate();
    }
    protected void Update() {
        changed.Validate();

        var dt = Time.deltaTime;

        var nf = forDebugData.Count;
        var nc = chars.Count;
        if (nf > nc) forDebugData.RemoveRange(nc, math.max(0, nf - nc));

        for (var i = 0; i < nc; i++) { 
            var ch = chars[i];
            float3 pos_world = ch.coord.Position;
            float3 forward_world = ch.coord.Forward;
            float3 force_total = default;
            var forDebug = (i < forDebugData.Count) ? forDebugData[i] : new DataForDebug();
            forDebug.index = i;
            forDebug.center_pos = pos_world;

            var boundary_force = GetBoundaryForce(ch, forDebug);
            force_total += boundary_force;

            //var wander_force = GetWanderForce(ch, forDebug);
            var wander_force = wander.GetWanderForce(ch.wanderData, ch.coord);
            Debug.Log($"wander={wander_force}");
            force_total += wander_force;

            var velocity = forward_world * tuner.speed;
            velocity += dt * force_total;
            var dir_forward3 = math.normalizesafe(velocity, forward_world);
            var rot = quaternion.LookRotationSafe(TR_Z, dir_forward3);
            ch.tr.localRotation = rot;
            pos_world += dt * tuner.speed * dir_forward3;
            ch.tr.position = new float3(pos_world.xy, 0f);

            forDebug.totalForce = force_total;
            if (forDebugData.Count <= i) forDebugData.Add(forDebug);
        }
    }
    private void OnRenderObject() {
        var c = Camera.current;
        if (!isActiveAndEnabled || (c.cullingMask & (1 << gameObject.layer)) == 0) return;

        var prop = new GLProperty() {
            Color = Color.red,
            ZTestMode = GLProperty.ZTestEnum.ALWAYS,
            ZWriteMode = false,
        };
        var wander = tuner.boid.wander;

        using (new GLMatrixScope()) {
            GL.modelview = c.worldToCameraMatrix;
            GL.LoadProjectionMatrix(GL.GetGPUProjectionMatrix(c.projectionMatrix, false));

            for (var i = 0; i < chars.Count; i++) {
                var ch = chars[i];

                using (new GLModelViewScope(ch.tr.localToWorldMatrix))
                using (gl.GetScope(new GLProperty(prop) { Color = Color.blue })) {
                    var wander_center = TR_Y * wander.distance;
                    GL.Begin(GL.LINE_STRIP);
                    //GL.Vertex(Vector3.zero);
                    GL.Vertex(wander_center);
                    GL.Vertex(wander_center + new float3(ch.wanderData.wanderTarget, 0f));
                    GL.End();
                }

                var ifor = forDebugData.FindIndex(v => v.index == i);
                if (ifor >= 0) {
                    var ch_debug = forDebugData[ifor];
                    var center = new float3(ch_debug.center_pos);
                    var search_r = new float3(ch_debug.search_pos_r, 0f);
                    var search_l = new float3(ch_debug.search_pos_l, 0f);
                    var boundary_r = new float3(ch_debug.boundary_pos_r, 0f);
                    var boundary_l = new float3(ch_debug.boundary_pos_l, 0f);

                    using (gl.GetScope(new GLProperty(prop) { Color = Color.gray })) {
                        GL.Begin(GL.LINES);
                        GL.Vertex(center);
                        GL.Vertex(center + new float3(ch_debug.totalForce));
                        GL.End();
                    }
                    using (gl.GetScope(new GLProperty(prop) { Color = Color.green })) {
                        GL.Begin(GL.LINES);
                        GL.Vertex(search_r);
                        GL.Vertex(boundary_r);
                        GL.Vertex(search_l);
                        GL.Vertex(boundary_l);
                        GL.End();
                    }
                }
            }
        }
    }
    #endregion

    #region methods
    public float3 GetBoundaryForce(CharacterInfo ch, DataForDebug ford) {
        var pos_world = ch.coord.Position.xy;
        var forward_world = ch.coord.Forward.xy;
        var right_world = ch.coord.Upward.xy;
        var search_world_r = pos_world + tuner.search_distance
            * math.lerp(forward_world, right_world, tuner.search_angle);
        var search_world_l = pos_world + tuner.search_distance
            * math.lerp(forward_world, -right_world, tuner.search_angle);

        var closest_dist_r = fields.SignedDistance(search_world_r, out var closest_pos_r);
        var closest_dist_l = fields.SignedDistance(search_world_l, out var closest_pos_l);
        float2 boundary_force = default;
        if (closest_dist_r > 1e-2f) {
            boundary_force += math.normalize(closest_pos_r - search_world_r)
                * math.smoothstep(0f, tuner.boundary_width, closest_dist_r) * tuner.boundary_power;
        }
        if (closest_dist_l > 1e-2f) {
            boundary_force += math.normalize(closest_pos_l - search_world_l)
                * math.smoothstep(0f, tuner.boundary_width, closest_dist_l) * tuner.boundary_power;
        }

        ford.search_pos_r = search_world_r;
        ford.search_pos_l = search_world_l;
        ford.boundary_pos_r = closest_pos_r;
        ford.boundary_pos_l = closest_pos_l;

        return new float3(boundary_force, 0f);
    }
    #endregion

    #region declarations
    public static readonly float3 TR_X = new float3(1f, 0f, 0f);
    public static readonly float3 TR_Y = new float3(0f, 1f, 0f);
    public static readonly float3 TR_Z = new float3(0f, 0f, 1f);

    public class TrCoordinates : Boid.ICoordinates {

        protected Transform tr;

        public TrCoordinates(Transform tr) {
            this.tr = tr;
        }

        public float3 Position => tr.position;
        public float3 Forward => tr.up;
        public float3 Right => tr.right;
        public float3 Upward => -tr.forward;
    }

    [System.Serializable]
    public class DataForDebug {
        public int index;
        public float3 totalForce;
        public float3 center_pos;

        public float2 search_pos_r;
        public float2 search_pos_l;
        public float2 boundary_pos_r;
        public float2 boundary_pos_l;
    }
    [System.Serializable]
    public class CharacterInfo {
        public Transform tr;
        public TrCoordinates coord;
        public Boid.WanderData wanderData;
    }
    [System.Serializable]
    public class Links {
        public Transform fab;
        public Transform fields;
    }

    [System.Serializable]
    public class Tuner {
        public int n = 1;
        public float speed;

        public float search_distance;
        [Range(0f, 1f)]
        public float search_angle;

        public float boundary_power;
        public float boundary_width;

        public Boid.Tuner boid = new Boid.Tuner();
    }
    #endregion
}