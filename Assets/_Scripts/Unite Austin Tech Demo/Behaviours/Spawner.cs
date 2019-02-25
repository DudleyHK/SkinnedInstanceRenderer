using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Unity.Entities;
using UnityEngine.Experimental.AI;
using Random = UnityEngine.Random;

public class Spawner : MonoBehaviour
{
	public static Spawner Instance;

	public static int Counter = 0;

	[NonSerialized]
	public bool SpawningFinished = false;

	EntityManager entityManager;

	NavMeshQuery mapLocationQuery;

	public void Awake()
	{
		Instance = this; // worst singleton ever but it works
		entityManager = World.Active.GetExistingManager<EntityManager>();
		var navMeshWorld = NavMeshWorld.GetDefaultWorld();
		mapLocationQuery = new NavMeshQuery(navMeshWorld, Allocator.Persistent);
	}

	public void OnDestroy()
	{
		Instance = null;
		mapLocationQuery.Dispose();
	}

	public unsafe Entity Spawn(FormationData formationData, float3 spawnPointOffset, FormationWaypoint[] waypoints, bool spawnedFromPortals = false, float3? forward = null)
	{
		var formationEntity = entityManager.CreateEntity();

		RaycastHit hit;

		if (Physics.Raycast(new Ray((Vector3)formationData.Position + new Vector3(0, 1000000, 0), Vector3.down), out hit))
		{
			formationData.Position.y = hit.point.y;
		}
		if (!spawnedFromPortals) formationData.SpawnedCount = formationData.UnitCount;

		if (forward == null)
		{
			formationData.Forward = new float3(0, 0, formationData.Position.z > 0 ? -1 : 1);
		}
		else
		{
			formationData.Forward = forward.Value;
		}

		formationData.HighLevelPathIndex = 1;
		entityManager.AddComponentData(formationEntity, formationData);
		entityManager.AddComponentData(formationEntity, new FormationClosestData());
		entityManager.AddComponentData(formationEntity, new FormationNavigationData { TargetPosition = formationData.Position });
        entityManager.AddComponent(formationEntity, typeof(EntityBuffer));
        entityManager.AddComponent(formationEntity, typeof(PolygonIdBuffer));
        entityManager.AddComponentData(formationEntity, new FormationIntegrityData());

		var unitType = (UnitType)formationData.UnitType;

		GameObject minionPrefab;
		minionPrefab = GetMinionPrefab(unitType);

		var prototypeMinion = entityManager.Instantiate(minionPrefab);

		entityManager.AddComponentData(prototypeMinion, new MinionBitmask(formationData.IsFriendly, spawnedFromPortals));
		entityManager.AddComponentData(prototypeMinion, new MinionAttackData(new Entity()));
		entityManager.AddComponentData(prototypeMinion, new MinionPathData());
		entityManager.AddComponent(prototypeMinion, typeof(PolygonIdBuffer));
		entityManager.AddComponentData(prototypeMinion, new IndexInFormationData(-1));
		entityManager.AddComponentData(prototypeMinion, new NavMeshLocationComponent());
		

		var minions = new NativeArray<Entity>(formationData.UnitCount, Allocator.Temp);
		entityManager.Instantiate(prototypeMinion, minions);

		for (int i = 0; i < minions.Length; ++i)
		{
			var entity = minions[i];
			var transform = entityManager.GetComponentData<UnitTransformData>(entity);
			var animator = entityManager.GetComponentData<TextureAnimatorData>(entity);
			var minion = entityManager.GetComponentData<MinionData>(entity);
			var indexInFormation = entityManager.GetComponentData<IndexInFormationData>(entity);

			transform.FormationEntity = formationEntity;
			indexInFormation.IndexInFormation = i;
			transform.UnitType = (int)unitType;
			transform.Forward = formationData.Forward;


            var randf3 = Random.insideUnitSphere * 100f;
            transform.Position = randf3;
            transform.Position.y = 0f;

			animator.UnitType = (int)unitType;
			animator.AnimationSpeedVariation = UnityEngine.Random.Range(SimulationSettings.Instance.MinionAnimationSpeedMin,
																		SimulationSettings.Instance.MinionAnimationSpeedMax);

			minion.attackCycle = -1;

			MinionPathData pathComponent = new MinionPathData()
			{
				bitmasks = 0,
				pathSize = 0,
				currentCornerIndex = 0
			};

			entityManager.SetComponentData(entity, pathComponent);

			entityManager.SetComponentData(entity, transform);
			entityManager.SetComponentData(entity, animator);
			entityManager.SetComponentData(entity, minion);
			entityManager.SetComponentData(entity, indexInFormation);
		}

		minions.Dispose();
		entityManager.DestroyEntity(prototypeMinion);

		return formationEntity;
	}

	public static GameObject GetMinionPrefab(UnitType unitType)
	{
		GameObject minionPrefab;
		switch (unitType)
		{
			case UnitType.Melee:
				minionPrefab = ViewSettings.Instance.MeleePrefab;
				break;
            case UnitType.Skeleton:
                minionPrefab = ViewSettings.Instance.SkeletonPrefab;
                break;
            default:
				throw new ArgumentOutOfRangeException("unitType", unitType, null);
		}
		return minionPrefab;
	}

	public void SpawnArrow(ArrowData arrowData)
	{
		var entity = entityManager.CreateEntity(typeof(ArrowData));
		entityManager.SetComponentData(entity, arrowData);
	}
}
