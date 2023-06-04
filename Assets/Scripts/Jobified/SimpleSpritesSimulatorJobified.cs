using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using vadersb.utils;
using vadersb.utils.unity.jobs;

public class SimpleSpritesSimulatorJobified : MonoBehaviour
{
    private struct Particle : IRenderable, IComparable<Particle>
    {
        public float2 m_Position;
        public Color m_Color;

        public bool m_HorLeft;
        public bool m_VerUp;

        public float m_SpeedMult;

        public int m_SpriteIndex;

        public float m_Angle;

        public float m_Scale;




        //-----
        //IRenderable

        public bool IsVisible()
        {
            return true;
        }

        public int GetSpriteIndex()
        {
            return m_SpriteIndex;
        }


        public float2 GetPosition()
        {
            return m_Position;
        }


        public float2 GetScale()
        {
            return new float2(m_Scale, m_Scale);
        }


        public float GetRotationAngle()
        {
            return m_Angle;
        }


        public Color GetColor()
        {
            return m_Color;
        }

        public int CompareTo(Particle other)
        {
            return other.m_Position.y.CompareTo(m_Position.y);
        }

        public readonly struct Comparer : IComparer<Particle>
        {
            public static readonly Comparer Defaut = new Comparer();

            public int Compare(Particle x, Particle y)
            {
                return x.CompareTo(y);
            }
        }
    }

    //settings
    [Header("Camera")]
    [SerializeField]
    private Camera m_Camera;

    [Header("Speed")]
    [SerializeField]
    private float m_SpeedMin = 0.05f;

    [SerializeField]
    private float m_SpeedMax = 1.0f;

    [Header("Scale")]
    [SerializeField]
    private float m_ScaleMin = 0.5f;

    [SerializeField]
    private float m_ScaleMax = 2.0f;

    [Header("Sprites count")]
    [SerializeField]
    private int m_SpritesCount = 1000;

    [Header("Optimization")]
    [SerializeField]
    [Range(1, 20000)]
    private int m_UpdateBatchCount = 64;

    [SerializeField]
    [Range(1, 20000)]
    private int m_VertexBatchCount = 1000;

    [SerializeField]
    [Range(1, 20000)]
    private int m_IndexBatchCount = 1000;


    [Header("DEBUG")]
    [SerializeField]
    private bool m_DebugUseFixedTimeDelta = true;

    private SpriteBatchRenderSetup m_BatchRenderSetup;
    private SpriteBatcher<Particle> m_SpriteBatcher;

    private NativeArray<Particle> m_Particles;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = (int)Screen.currentResolution.refreshRate / Application.targetFrameRate;

        m_BatchRenderSetup = GetComponent<SpriteBatchRenderSetup>();
        Debug.Assert(m_BatchRenderSetup != null);
    }


    private void Start()
    {
        m_SpriteBatcher = new SpriteBatcher<Particle>(m_BatchRenderSetup.Mesh);

        GenerateParticles();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z) == true)
        {
            m_DebugUseFixedTimeDelta = !m_DebugUseFixedTimeDelta;
            //UpdateDeltaTimeMark();
        }

        float minX = -1.0f;
        float maxX = 1.0f;
        float minY = -1.0f;
        float maxY = 1.0f;

        float deltaTime = Time.deltaTime;

        //getting camera bounds
        if (m_Camera)
        {
            float halfSize = m_Camera.orthographicSize;
            float ratio = m_Camera.aspect;

            minY = -halfSize;
            maxY = halfSize;

            minX = -halfSize * ratio;
            maxX = halfSize * ratio;
        }


        //particles update job
        var updateJob = new ParticlesUpdateJob(
              m_Particles
            , new float2(minX, maxX)
            , new float2(minY, maxY)
            , deltaTime
            , m_SpeedMin
            , m_SpeedMax
        );

        var jobHandle = updateJob.Schedule(m_Particles.Length, m_UpdateBatchCount);

        var sortJob = NativeSortExtension.SortJob(m_Particles, Particle.Comparer.Defaut);

        jobHandle = sortJob.Schedule(jobHandle);

        //sprite batcher begin
        m_SpriteBatcher.BatchStart(
            m_Particles
            , m_Particles.Length
            , m_BatchRenderSetup.SpriteDataArray
            , jobHandle
            , m_VertexBatchCount
            , m_IndexBatchCount
        );
    }


    private void LateUpdate()
    {
        //sprite batcher finalize
        m_SpriteBatcher.BatchFinalize();

        //todo a good place to run RemoveDeadJob (it will execute while rendering is being done, Complete() will be called in the *next* frame Update(), so it happens kinda "between frames")
    }


    private void OnDestroy()
    {
        m_Particles.Dispose();
    }


    private void GenerateParticles()
    {
        WeightedRandomizer<Color> randomizer = new WeightedRandomizer<Color>();

        randomizer.AddValue(new Color32(113, 209, 129, 255), 1.0f);
        randomizer.AddValue(new Color32(250, 128, 114, 255), 1.0f);
        randomizer.AddValue(new Color32(66, 120, 217, 255), 1.0f);
        randomizer.AddValue(new Color32(167, 173, 175, 255), 1.0f);
        randomizer.AddValue(new Color32(206, 210, 136, 255), 1.0f);

        float minX = -3.0f;
        float maxX = 3.0f;
        float minY = -3.0f;
        float maxY = 3.0f;

        //getting camera bounds
        if (m_Camera)
        {
            float halfSize = m_Camera.orthographicSize;
            float ratio = m_Camera.aspect;

            minY = -halfSize;
            maxY = halfSize;

            minX = -halfSize * ratio;
            maxX = halfSize * ratio;
        }

        //generate particles
        m_Particles = new NativeArray<Particle>(m_SpritesCount, Allocator.Persistent);

        for (int i = 0; i < m_SpritesCount; i++)
        {
            var particle = new Particle();
            var speedMult = MathHelpers.Random_Factor();

            particle.m_Position.x = MathHelpers.Random_Float(minX, maxX);
            particle.m_Position.y = MathHelpers.Random_Float(minY, maxY);
            particle.m_Color = randomizer.GetRandomValue();

            particle.m_HorLeft = MathHelpers.Random_CheckChance(0.5f);
            particle.m_VerUp = MathHelpers.Random_CheckChance(0.5f);
            particle.m_SpeedMult = speedMult;
            particle.m_SpeedMult = particle.m_SpeedMult * speedMult * speedMult * speedMult * speedMult * speedMult * speedMult;
            particle.m_SpriteIndex = i % 3;
            //particle.m_Angle = MathHelpers.Random_Angle();

            particle.m_Scale = Interpolation.Linear(0.5f, 2.0f, (i + 1.0f) / m_SpritesCount);

            m_Particles[i] = particle;
        }
    }




    //update job
    [BurstCompile]
    private struct ParticlesUpdateJob : IJobParallelFor
    {
        //-----
        //input data
        private NativeArray<Particle> m_Particles;

        private float2 m_BoundsX;
        private float2 m_BoundsY;

        private float m_DeltaTime;

        private float m_SpeedMin;
        private float m_SpeedMax;


        public ParticlesUpdateJob(NativeArray<Particle> particles, float2 boundsX, float2 boundsY, float deltaTime, float speedMin, float speedMax)
        {
            m_Particles = particles;

            m_BoundsX = boundsX;
            m_BoundsY = boundsY;

            m_DeltaTime = deltaTime;

            m_SpeedMin = speedMin;
            m_SpeedMax = speedMax;
        }


        public void Execute(int index)
        {
            var particle = m_Particles[index];

            //speed
            float curSpeed = math.lerp(m_SpeedMin, m_SpeedMax, particle.m_SpeedMult) * m_DeltaTime;

            //moving and bouncing
            if (particle.m_HorLeft == true)
            {
                particle.m_Position.x -= curSpeed;
                if (particle.m_Position.x <= m_BoundsX.x)
                {
                    particle.m_HorLeft = false;
                }
            }
            else
            {
                particle.m_Position.x += curSpeed;
                if (particle.m_Position.x >= m_BoundsX.y)
                {
                    particle.m_HorLeft = true;
                }
            }

            //ver
            if (particle.m_VerUp == true)
            {
                particle.m_Position.y += curSpeed;
                if (particle.m_Position.y >= m_BoundsY.y)
                {
                    particle.m_VerUp = false;
                }
            }
            else
            {
                particle.m_Position.y -= curSpeed;
                if (particle.m_Position.y <= m_BoundsY.x)
                {
                    particle.m_VerUp = true;
                }
            }

            //rotation
            //todo expand
            //particle.m_Angle += 0.2f * m_DeltaTime;


            //finally - storing the updated particle
            m_Particles[index] = particle;
        }
    }

}
