using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Aplib.Core.Belief.Beliefs;
using Aplib.Core.Belief.BeliefSets;
using Aplib.Core.Desire.Goals;
using Aplib.Core.Intent.Actions;
using Aplib.Core.Intent.Tactics;
using Aplib.Integrations.Unity.Actions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Complete;
using Aplib.Core.Agents;
using Aplib.Core.Desire.DesireSets;
using Aplib.Core.Desire.GoalStructures;
using Aplib.Integrations.Unity;
using Aplib.Core;
using static UnityEngine.GraphicsBuffer;
using UnityEngine.AI;


public class KillEnemyBeliefSet : BeliefSet
{
    public GameObject Player = FindPlayer(1);

    public GameObject Enemy = FindPlayer(2);

    public TankShooting PlayerShooting = FindPlayer(1).GetComponent<TankShooting>();


    /////////////////////////////////////////////////////////////////////////////////////////////
    /// Define beliefs that store the positions of the player and the enemy tank.
    /////////////////////////////////////////////////////////////////////////////////////////////
    public Belief<Transform, Vector3> PlayerPosition = null;

    public Belief<Transform, Vector3> EnemyPosition = null;

    private static GameObject FindPlayer(int playerID)
    {
        TankMovement[] tanks = Object.FindObjectsByType<TankMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return tanks.FirstOrDefault(tank => tank.m_PlayerNumber == playerID).gameObject;
    }
}

public class KillEnemyTest
{
    [SetUp]
    public void Setup()
    {
        // Load the scene
        SceneManager.LoadScene("_Complete-Game");
    }

    /// <summary>
    /// This test:
    /// 1. Drives the player tank to the enemy tank.
    /// 2. Rotates the player tank to face the enemy tank.
    /// 3. Fires the player tank's cannon.
    /// 4. Repeats until the enemy tank is destroyed.
    /// </summary>
    /// <returns></returns>
    [UnityTest]
    public IEnumerator KillEnemyTestWithEnumeratorPasses()
    {
        KillEnemyBeliefSet beliefSet = new KillEnemyBeliefSet();

        // Actions:
        /////////////////////////////////////////////////////////////////////////////////////////////
        /// Implement this action to drive the player tank to the enemy tank.
        /////////////////////////////////////////////////////////////////////////////////////////////
        TransformPathfinderAction<KillEnemyBeliefSet> driveToEnemyAction = null;

        Action<KillEnemyBeliefSet> rotateToEnemyAction = new(beliefSet =>
        {
            Vector3 playerPosition = beliefSet.PlayerPosition;
            Vector3 enemyPosition = beliefSet.EnemyPosition;

            Vector3 direction = enemyPosition - playerPosition;

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            beliefSet.Player.transform.rotation = Quaternion.RotateTowards(beliefSet.Player.transform.rotation, targetRotation, 1);
        });

        Action<KillEnemyBeliefSet> beginChargeCannonAction = new(beliefSet => beliefSet.PlayerShooting.BeginCharge());
        Action<KillEnemyBeliefSet> chargeCannonAction = new(beliefSet => beliefSet.PlayerShooting.Charge());
        Action<KillEnemyBeliefSet> fireCannonAction = new(beliefSet => beliefSet.PlayerShooting.Fire());

        PrimitiveTactic<KillEnemyBeliefSet> driveToEnemyTactic = new(action: driveToEnemyAction);

        PrimitiveTactic<KillEnemyBeliefSet> rotateToEnemyTactic = new(action: rotateToEnemyAction, guard: ShouldRotateToEnemy);
        PrimitiveTactic<KillEnemyBeliefSet> beginChargeCannonTactic = new(action: chargeCannonAction, guard: ShouldBeginChargeCannon);
        PrimitiveTactic<KillEnemyBeliefSet> chargeCannonTactic = new(action: chargeCannonAction, guard: ShouldChargeCannon);
        PrimitiveTactic<KillEnemyBeliefSet> fireCannonTactic = new(action: fireCannonAction);

        /////////////////////////////////////////////////////////////////////////////////////////////
        /// Define the killEnemyTactic using the tactics defined above.
        /// You also have to choose the appropriate tactic combinator.
        /// Choose between PrimitiveTactic, FirstOfTactic, and AnyOfTactic
        /// Note: you will have to change the type of the tactic variable.
        /////////////////////////////////////////////////////////////////////////////////////////////
        ITactic<KillEnemyBeliefSet> killEnemyTactic = null;


        // Goals:
        Goal<KillEnemyBeliefSet> driveToEnemyGoal = new(tactic: driveToEnemyTactic, predicate: IsEnemyInLineOfSight);
        
        Goal<KillEnemyBeliefSet> killEnemyGoal = new(tactic: killEnemyTactic, predicate: IsEnemyDestroyed);

        // Goal structures:
        PrimitiveGoalStructure<KillEnemyBeliefSet> driveToEnemyGoalStructure = new(driveToEnemyGoal);

        PrimitiveGoalStructure<KillEnemyBeliefSet> killEnemyGoalStructure = new(killEnemyGoal);

        /////////////////////////////////////////////////////////////////////////////////////////////
        /// Define the mainGoal using the goal structures defined above.
        /// You also have to choose the appropriate goal structure combinator.
        /// Choose between PrimitiveGoalStructure, SequentialGoalStructure, and RepeatGoalStructure.
        /// Note: you will have to change the type of the goal structure variable.
        /////////////////////////////////////////////////////////////////////////////////////////////
        IGoalStructure<KillEnemyBeliefSet> mainGoal = null;

        DesireSet<KillEnemyBeliefSet> desire = new(mainGoal);

        // Create a new agent with the goal
        BdiAgent<KillEnemyBeliefSet> agent = new(beliefSet, desire);

        AplibRunner testRunner = new(agent);

        // Run the test.
        yield return testRunner.Test();

        // Assert the status of the main goal.
        Assert.IsTrue(condition: agent.Status == CompletionStatus.Success);
        yield break;

        bool IsEnemyInLineOfSight(KillEnemyBeliefSet beliefSet)
        {
            bool isInRange = Vector3.Distance(beliefSet.PlayerPosition, beliefSet.EnemyPosition) < 25;

            NavMeshPath path = new();
            NavMesh.CalculatePath(
                beliefSet.Player.GetComponent<Rigidbody>().position,
                beliefSet.EnemyPosition,
                NavMesh.AllAreas,
                path
            );

            bool isStraightPath = path.corners.Length == 2;

            return isInRange && isStraightPath;
        }

        bool IsEnemyDestroyed(KillEnemyBeliefSet beliefSet) => !beliefSet.Enemy.activeInHierarchy;

        bool ShouldRotateToEnemy(KillEnemyBeliefSet beliefSet)
        {
            Vector3 playerPosition = beliefSet.PlayerPosition;
            Vector3 enemyPosition = beliefSet.EnemyPosition;

            Vector3 direction = enemyPosition - playerPosition;

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            return Quaternion.Angle(beliefSet.Player.transform.rotation, targetRotation) > 1;
        }

        bool ShouldBeginChargeCannon(KillEnemyBeliefSet beliefSet)
            => beliefSet.PlayerShooting.m_CurrentLaunchForce == beliefSet.PlayerShooting.m_MinLaunchForce;

        bool ShouldChargeCannon(KillEnemyBeliefSet beliefSet)
        {
            float chargeLaunchForce = beliefSet.PlayerShooting.m_MinLaunchForce + (beliefSet.PlayerShooting.m_MaxLaunchForce - beliefSet.PlayerShooting.m_MinLaunchForce) / 2;
            return beliefSet.PlayerShooting.m_CurrentLaunchForce < chargeLaunchForce;
        }
    }
}
