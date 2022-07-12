using System.Collections;
using UnityEngine;
using DG.Tweening;
using Cinemachine;
using UnityEngine.Events;

public class CombatScript : MonoBehaviour
{
    private EnemyManager enemyManager;
    private EnemyDetection enemyDetection;
    private MovementInput movementInput;
    private Animator animator;
    //private CinemachineImpulseSource impulseSource;

    [Header("Target")]
    private EnemyScript lockedTarget;

    [Header("Combat Settings")]
    [SerializeField] private float attackCooldown;

    [Header("States")]
    public bool isAttackingEnemy = false;
    public bool isCountering = false;
    public bool canPunch = true;

    //[Header("Public References")]
    //[SerializeField] private Transform punchPosition;
    //[SerializeField] private ParticleSystemScript punchParticle;
    //[SerializeField] private GameObject lastHitCamera;
    //[SerializeField] private Transform lastHitFocusObject;

    //Coroutines
    private Coroutine counterCoroutine;
    private Coroutine damageCoroutine;

    [Space]

    //Events
    public UnityEvent<EnemyScript> OnTrajectory;
    public UnityEvent<EnemyScript> OnHit;
    public UnityEvent<EnemyScript> OnCounterAttack;

    int punchCount = 0;

    void Start()
    {
        enemyManager = FindObjectOfType<EnemyManager>();
        animator = GetComponent<Animator>();
        enemyDetection = GetComponentInChildren<EnemyDetection>();
        movementInput = GetComponent<MovementInput>();
        //impulseSource = GetComponentInChildren<CinemachineImpulseSource>();
    }

    private void Update()
    {
        OnAttack();
    }

    //This function gets called whenever the player inputs the punch action
    void AttackCheck()
    {
        if (isAttackingEnemy)
            return;

        //Check to see if the detection behavior has an enemy set
        if (enemyDetection.CurrentTarget() == null)
        {
            if (enemyManager.AliveEnemyCount() == 0)
            {
                Attack(null, 0);
                return;
            }
            else
            {
                lockedTarget = enemyManager.RandomEnemy();
            }
        }

        //If the player is moving the movement input, use the "directional" detection to determine the enemy
        if (enemyDetection.InputMagnitude() > .2f)
            lockedTarget = enemyDetection.CurrentTarget();

        //Extra check to see if the locked target was set
        if (lockedTarget == null)
            lockedTarget = enemyManager.RandomEnemy();

        //AttackTarget
        Attack(lockedTarget, TargetDistance(lockedTarget));
    }

    public void Attack(EnemyScript target, float distance)
    {
        //Attack nothing in case target is null
        if (target == null)
        {
            AttackType(.2f, null, 0);
            return;
        }

        if (distance < 15)
        {
            AttackType(attackCooldown, target, .65f);
        }
        else
        {
            lockedTarget = null;
            AttackType(.2f, null, 0);
        }

        //Change impulse
        //impulseSource.m_ImpulseDefinition.m_AmplitudeGain = Mathf.Max(3, 1 * distance);

    }

    void AttackType(float cooldown, EnemyScript target, float movementDuration)
    {
        if (canPunch && punchCount <= 3)
        {
            punchCount++;
        }

        if (punchCount == 1)
        {
            animator.SetInteger("Punch", 1);
            movementInput.acceleration = 0;
            movementInput.enabled = false;

            MoveToTarget();
        }
    }

    void MoveToTarget()
    {
        if (lockedTarget == null)
            return;

        lockedTarget.StopMoving();
        MoveTorwardsTarget(lockedTarget, .5f);
    }

    public void CheckLightCombo()
    {
        isAttackingEnemy = true;
        canPunch = false;
        if (animator.GetCurrentAnimatorStateInfo(0).IsName("LightPunch_1") && punchCount <= 1)
        {
            EndLightPunchCombo();
        }
        else if (animator.GetCurrentAnimatorStateInfo(0).IsName("LightPunch_1") && punchCount >= 2)
        {
            animator.SetInteger("Punch", 2);
            MoveToTarget();
            canPunch = true;
        }
        else if (animator.GetCurrentAnimatorStateInfo(0).IsName("LightPunch_2") && punchCount <= 2)
        {
            EndLightPunchCombo();
        }
        else if (animator.GetCurrentAnimatorStateInfo(0).IsName("LightPunch_2") && punchCount >= 3)
        {
            animator.SetInteger("Punch", 3);
            MoveToTarget();
            canPunch = false;
        }
        else if (animator.GetCurrentAnimatorStateInfo(0).IsName("LightPunch_3"))
        {
            EndLightPunchCombo();
        }

    }

    void EndLightPunchCombo()
    {
        animator.SetInteger("Punch", 0);
        punchCount = 0;
        canPunch = true;
        movementInput.enabled = true;
        isAttackingEnemy = false;
        LerpCharacterAcceleration();
    }

    void MoveTorwardsTarget(EnemyScript target, float duration)
    {
        OnTrajectory.Invoke(target);
        transform.DOLookAt(target.transform.position, .2f);
        transform.DOMove(TargetOffset(target.transform), duration);
    }

    void CounterCheck()
    {
        //Initial check
        if (isCountering || isAttackingEnemy || !enemyManager.AnEnemyIsPreparingAttack())
            return;

        lockedTarget = ClosestCounterEnemy();
        OnCounterAttack.Invoke(lockedTarget);

        if (TargetDistance(lockedTarget) > 2)
        {
            Attack(lockedTarget, TargetDistance(lockedTarget));
            return;
        }

        float duration = .2f;
        animator.SetTrigger("Dodge");
        transform.DOLookAt(lockedTarget.transform.position, .2f);
        transform.DOMove(transform.position + lockedTarget.transform.forward, duration);

        if (counterCoroutine != null)
            StopCoroutine(counterCoroutine);
        counterCoroutine = StartCoroutine(CounterCoroutine(duration));

        IEnumerator CounterCoroutine(float duration)
        {
            isCountering = true;
            movementInput.enabled = false;
            yield return new WaitForSeconds(duration);
            Attack(lockedTarget, TargetDistance(lockedTarget));
            isCountering = false;

        }
    }

    float TargetDistance(EnemyScript target)
    {
        return Vector3.Distance(transform.position, target.transform.position);
    }

    public Vector3 TargetOffset(Transform target)
    {
        Vector3 position;
        position = target.position;
        return Vector3.MoveTowards(position, transform.position, .95f);
    }

    public void HitEvent()
    {
        if (lockedTarget == null || enemyManager.AliveEnemyCount() == 0)
            return;

        OnHit.Invoke(lockedTarget);

        //Polish
        //punchParticle.PlayParticleAtPosition(punchPosition.position);
    }

    public void DamageEvent()
    {
        animator.SetTrigger("Hit");

        if (damageCoroutine != null)
            StopCoroutine(damageCoroutine);
        damageCoroutine = StartCoroutine(DamageCoroutine());

        IEnumerator DamageCoroutine()
        {
            movementInput.enabled = false;
            yield return new WaitForSeconds(.5f);
            movementInput.enabled = true;
            LerpCharacterAcceleration();
        }
    }

    EnemyScript ClosestCounterEnemy()
    {
        float minDistance = 100;
        int finalIndex = 0;

        for (int i = 0; i < enemyManager.allEnemies.Length; i++)
        {
            EnemyScript enemy = enemyManager.allEnemies[i].enemyScript;

            if (enemy.IsPreparingAttack())
            {
                if (Vector3.Distance(transform.position, enemy.transform.position) < minDistance)
                {
                    minDistance = Vector3.Distance(transform.position, enemy.transform.position);
                    finalIndex = i;
                }
            }
        }

        return enemyManager.allEnemies[finalIndex].enemyScript;

    }

    void LerpCharacterAcceleration()
    {
        movementInput.acceleration = 0;
        DOVirtual.Float(0, 1, .6f, ((acceleration) => movementInput.acceleration = acceleration));
    }

    bool isLastHit()
    {
        if (lockedTarget == null)
            return false;

        return enemyManager.AliveEnemyCount() == 1 && lockedTarget.health <= 1;
    }

    #region Input

    private void OnCounter()
    {
        CounterCheck();
    }

    private void OnAttack()
    {
        if(Input.GetKeyDown(KeyCode.J))
            AttackCheck();
    }

    #endregion

}