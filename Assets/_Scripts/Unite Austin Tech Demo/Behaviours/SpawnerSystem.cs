﻿using System.Collections.Generic;
using UnityEngine;
using System;
using System.Diagnostics;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Jobs;
using Object = UnityEngine.Object;

// [UpdateAfter(typeof(UnitLifecycleManager))]
public class SpawnerSystem : JobComponentSystem
{
	public List<List<float3>> newHighLevelRoutes = new List<List<float3>>();

	private Spawner spawnerObject;

	public SpawnPointComponent[] SpawnPoints;

	public FormationWaypoint[] formationWaypoints;

	private TextureAnimatorSystem textureAnimatorSystem;

	public int totalRequested = 0;
	private bool initialized = false;

    
	private void InitializeSpawnPoints()
	{
		if (spawnerObject == null)
		{
			spawnerObject = GameObject.FindObjectOfType<Spawner>();
		}

		InitialSpawning();

		SpawnPointComponent[] spawnComponents = GameObject.FindObjectsOfType<SpawnPointComponent>();
		SpawnPoints = spawnComponents;
		for (int i = 0; i < spawnComponents.Length; i++)
		{
			SpawnPoints[i] = spawnComponents[i];
			var spawnPointData = SpawnPoints[i];
			spawnPointData.formationEntity = new Entity(); // does new Entity() behave like a null entity
			spawnPointData.offset = SpawnPoints[i].transform.GetChild(0).transform.position - SpawnPoints[i].transform.position;

			spawnPointData.Position = spawnComponents[i].transform.position;
			spawnPointData.Position.y = 0;
			spawnPointData.ElapsedTime = SimulationSettings.Instance.SpawnTimesPerUnit[(int)spawnPointData.Type];
			spawnPointData.ElapsedTimeSinceLastBatch = SimulationSettings.Instance.TimeToSpawnBatch;
			SpawnPoints[i] = spawnPointData;
		}

		//spawnerObject.SpawningFinished = true;
	}

	public void InitialSpawning()
	{
		ScriptedSpawner[] scriptedSpawners = GameObject.FindObjectsOfType<ScriptedSpawner>();

		List<ScriptedSpawner> spawners = new List<ScriptedSpawner>(scriptedSpawners);
		spawners.Sort((a, b) => a.Priority.CompareTo(b.Priority));
		scriptedSpawners = spawners.ToArray();

		// spawn less formations if p2gc is not enabled;


		for (int i = 0; i < scriptedSpawners.Length; ++i)
		{
			var spawner = scriptedSpawners[i];

			if (spawner.InactiveAtStart) continue;

			int spawned = spawner.Spawn(totalRequested);
			totalRequested += spawned;
		}

	}

	public void SpawnAdditionalUnits()
	{
		var spawners = GameObject.FindObjectsOfType<ScriptedSpawner>();

		foreach (var spawner in spawners)
		{
			if (!spawner.InactiveAtStart) continue;
			int spawned = spawner.Spawn(totalRequested);
			totalRequested += spawned;
		}
	}


	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
		if (!initialized)
		{
			Initialize();
		}

		// Schedule instantiation
		if (Input.GetKeyDown(KeyCode.S))
		{
			SpawnAdditionalUnits();
		}

		if (!SimulationSettings.Instance.EnableSpawners)
			return inputDeps;

		if (SpawnPoints.Length == 0)
			return inputDeps;

		inputDeps.Complete();
		// Enable spawn point to spawn units in time slots
		for (int i = 0; i < SpawnPoints.Length; i++)
		{
			SpawnPointComponent point = SpawnPoints[i];

			float spawnTime = SimulationSettings.Instance.SpawnTimesPerUnit[(int)point.Type];

			// spawn point is ready to spawn units
			if (point.ElapsedTime >= spawnTime)
			{
				point.ElapsedTimeSinceLastBatch = point.ElapsedTimeSinceLastBatch + Time.deltaTime;

				// Ready to spawn. Create new formation data
				if (point.NumberOfSpawnedUnits == 0)
				{
					if (textureAnimatorSystem.initialized && textureAnimatorSystem.perUnitTypeDataHolder[point.Type].Count
						+ SimulationSettings.Instance.UnitsPerFormation >= 34000)
					{
						continue;
					}
					if (textureAnimatorSystem.initialized) textureAnimatorSystem.perUnitTypeDataHolder[point.Type].Count = textureAnimatorSystem.perUnitTypeDataHolder[point.Type].Count
																															+ SimulationSettings.Instance.UnitsPerFormation;

					var entity = Spawn(point);

					var forData = EntityManager.GetComponentData<FormationData>(entity);
					forData.FormationState = FormationData.State.Spawning;
					point.NumberOfSpawnedUnits = point.NumberOfSpawnedUnits + SimulationSettings.countPerSpawner;
					forData.SpawnedCount = forData.SpawnedCount + SimulationSettings.countPerSpawner;

					EntityManager.SetComponentData(entity, forData);
					point.formationEntity = entity;

					point.SpawnedFormationCount++;
				}
				else
				{
					var formationsFromEntity = GetComponentDataFromEntity<FormationData>();
					var formation = formationsFromEntity[point.formationEntity];
					if (point.ElapsedTimeSinceLastBatch >= SimulationSettings.Instance.TimeToSpawnBatch)
					{
						point.ElapsedTimeSinceLastBatch = point.ElapsedTimeSinceLastBatch - SimulationSettings.Instance.TimeToSpawnBatch;
						point.NumberOfSpawnedUnits = point.NumberOfSpawnedUnits + SimulationSettings.countPerSpawner;
						formation.SpawnedCount = formation.SpawnedCount + SimulationSettings.countPerSpawner;
					}
					if (point.NumberOfSpawnedUnits >= SimulationSettings.Instance.UnitsPerFormation)
					{
						point.NumberOfSpawnedUnits = 0;
						point.ElapsedTime = point.ElapsedTime - spawnTime;
						formation.FormationState = FormationData.State.Walking;
					}
					formationsFromEntity[point.formationEntity] = formation;
				}
			}
			else
			{
				point.ElapsedTime = point.ElapsedTime + Time.deltaTime;
			}
			SpawnPoints[i] = point;
		}
		return new JobHandle();
	}

	public void Initialize()
	{
		initialized = true;
		formationWaypoints = Object.FindObjectsOfType<FormationWaypoint>();

        textureAnimatorSystem = World.GetExistingManager<TextureAnimatorSystem>();

		InitializeSpawnPoints();
	}

	public Entity Spawn(SpawnPointComponent point)
	{
		var settings = SimulationSettings.Instance;

		int unitCount = SimulationSettings.Instance.UnitsPerFormation;
		var formationData = new FormationData(unitCount, settings.FormationWidth, point.IsFriendly, point.Type, point.Position);
		//formationData.EnableMovement = false;

		var formationEntity = spawnerObject.Spawn(formationData, point.offset, formationWaypoints, true);

		++totalRequested;
		return formationEntity;
	}

	public const float initialPosition = 100;
	public const float forwardStep = 9f;
	public const float sidewaysStep = 17f;
	public const int width = 24;
	public const float initialSeparation = 50f;

	private void SpawnFormation()
	{
		var settings = SimulationSettings.Instance;

		var isFriendly = totalRequested % 2 == 0;

		float3 position = new float3((totalRequested / 2) % width / 2f * sidewaysStep * (totalRequested / 2 % 2 == 0 ? -1 : 1)
			- ((totalRequested % 4 >= 2) ? sidewaysStep * 0.5f : 0),
			0f, ((totalRequested) / (width * 2) * forwardStep + initialPosition) * (isFriendly ? 1 : -1));

		int typeBase = (totalRequested / (width * 2)) % 2;
		var unitType = (UnitType)(typeBase * 2 + totalRequested % 2);

		int unitCount = SimulationSettings.Instance.UnitsPerFormation;
		var formationData = new FormationData(unitCount, settings.FormationWidth, isFriendly, unitType, position);
		spawnerObject.Spawn(formationData, new float3(0, 0, 0), formationWaypoints);

		++totalRequested;
	}
}
