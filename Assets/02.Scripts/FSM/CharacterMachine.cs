using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public enum State
{
    None,
    Idle,
    Move,
    Jump,
    SecondJump,
    Fall,
    Land,
    Crouch,
    LadderClimbing,
    Ledge,
    LedgeClimb,
    WallSlide,
    Attack,
    Hurt,
    Die
}

public abstract class CharacterMachine : MonoBehaviour, IHp
{
    // Direction
    public int direction
    {
        get => _direction;
        set
        {
            if (isDirectionChangeable == false)
            {
                return;
            }

            if (_direction == value)
            {
                return;
            }

            if (value > 0)
            {
                transform.eulerAngles = Vector3.zero;
                _direction = DIRECTION_RIGHT;
            }
            else if (value < 0)
            {
                transform.eulerAngles = Vector3.up * 180.0f;
                _direction = DIRECTION_LEFT;
            }
            else
            {
                throw new System.Exception("[CharacterMachine] Invalid direction value: " + value);
            }
            
            onDirectionChanged?.Invoke(_direction);
        }
    }

    private int _direction = DIRECTION_RIGHT;
    [HideInInspector] public bool isDirectionChangeable;
    
    public event Action<int> onDirectionChanged;

    public const int DIRECTION_RIGHT = 1;
    public const int DIRECTION_LEFT = -1;
    public const int DIRECTION_UP = 1;
    public const int DIRECTION_DOWN = -1;

    // Movement
    public virtual float horizontal { get; set; }
    public virtual float vertical { get; set; }

    public float speed;
    [HideInInspector] public Vector2 move;
    [HideInInspector] public bool isMovable;
    private Rigidbody2D _rigidbody;

    public State current;
    public State previous;
    private Dictionary<State, IWorkflow<State>> _states;
    private bool _isDirty;

    public Animator animator;

    // Flags
    public bool hasJumped;
    public bool hasSecondJumped;

    public bool isGrounded
    {
        get
        {
            return Physics2D.OverlapBox(_rigidbody.position + _groundDetectCenter, _groundDetectSize, 0.0f,
                _groundMask);
        }
    }

    [Header("Ground Detection")] [SerializeField]
    private Vector2 _groundDetectCenter;

    [SerializeField] private Vector2 _groundDetectSize;
    [SerializeField] private LayerMask _groundMask;

    // Ladder detection
    public bool canLadderUp { get; private set; }
    public bool canLadderDown { get; private set; }
    public Ladder upLadder;
    public Ladder downLadder;

    [SerializeField] private float _ladderUpDetectOffset;
    [SerializeField] private float _ladderDownDetectOffset;
    [SerializeField] private float _ladderDetectRadius;
    [SerializeField] private LayerMask _ladderMask;

    // Ledge detection
    public bool isLedgeDetected;
    public Vector2 ledgePoint;
    public Vector2 ledgeDetectOffset;
    [SerializeField] private float _ledgeDetectDistance;
    [SerializeField] private LayerMask _ledgeMask;

    // Wall detection
    public bool isWallDetected;
    [SerializeField] private float _wallTopDetectHeight;
    [SerializeField] private float _wallBottomDetectHeight;
    [SerializeField] private float _wallDetectDistance;
    [SerializeField] private LayerMask _wallMask;

    public bool isInvincible { get; set; }
    
    // Hp
    public float hpValue
    {
        get => _hpValue;
        private set
        {
            if (value == _hpValue)
                return;

            if (value > _hpMax)
                value = _hpMax;
            else if (value < hpMin)
                value = hpMin;

            _hpValue = value;
            onHpChanged?.Invoke(value);
            
            if (value == hpMax)
                onHpMax?.Invoke();
            else if (value == hpMin)
                onHpMin?.Invoke();
        }
    }
    public float hpMax => _hpMax;
    public float hpMin => 0.0f;

    private float _hpValue;
    [SerializeField] private float _hpMax;
    
    public void RecoverHp(object subject, float amount)
    {
        if (amount <= 0)
            return;

        hpValue += amount;
        onHpRecovered?.Invoke(amount);
    }

    public void DepleteHp(object subject, float amount)
    {
        if (isInvincible)
            return;
        
        if (amount <= 0)
            return;

        hpValue -= amount;
        onHpDepleted?.Invoke(amount);
    }

    public event Action<float> onHpChanged;
    public event Action<float> onHpRecovered;
    public event Action<float> onHpDepleted;
    public event Action onHpMax;
    public event Action onHpMin;

    public float attackForceMin;
    public float attackForceMax;

    public void Initialize(IEnumerable<KeyValuePair<State, IWorkflow<State>>> copy)
    {
        _states = new Dictionary<State, IWorkflow<State>>(copy);
        current = copy.First().Key;
    }

    public bool ChangeState(State newState, object[] parameters = null)
    {
        if (_isDirty)
        {
            return false;
        }

        if (newState == current)
        {
            return false;
        }

        if (_states[newState].CanExecute == false)
        {
            return false;
        }

        _states[current].OnExit();
        previous = current;
        current = newState;
        _states[newState].OnEnter(parameters);
        _isDirty = true;
        return true;
    }
    
    public void Knockback(Vector2 force)
    {
        _rigidbody.velocity = Vector2.zero;
        _rigidbody.AddForce(force, ForceMode2D.Impulse);
    }

    protected virtual void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _direction = DIRECTION_RIGHT;
        _hpValue = _hpMax;
    }

    protected virtual void Update()
    {
        ChangeState(_states[current].OnUpdate());

        if (isMovable)
        {
            move = new Vector2(horizontal * speed, 0.0f);
        }

        if (isDirectionChangeable && Mathf.Abs(horizontal) > 0.0f)
        {
            direction = horizontal < 0.0f ? DIRECTION_LEFT : DIRECTION_RIGHT;
        }
    }

    private void FixedUpdate()
    {
        DetectLadder();
        DetectLedge();
        DetectWall();
        _states[current].OnFixedUpdate();
        _rigidbody.position += move * Time.fixedDeltaTime;
    }

    private void LateUpdate()
    {
        _isDirty = false;
    }

    private void DetectLadder()
    {
        Collider2D upCol = Physics2D.OverlapCircle(_rigidbody.position + Vector2.up * _ladderUpDetectOffset,
            _ladderDetectRadius, _ladderMask);

        upLadder = upCol ? upCol.GetComponent<Ladder>() : null;
        canLadderUp = upLadder;

        Collider2D downCol =
            Physics2D.OverlapCircle(_rigidbody.position + Vector2.up * _ladderDownDetectOffset, _ladderDetectRadius,
                _ladderMask);

        downLadder = downCol ? downCol.GetComponent<Ladder>() : null;
        canLadderDown = downLadder;
    }

    private void DetectLedge()
    {
        RaycastHit2D hit = Physics2D.Raycast(
            _rigidbody.position + new Vector2(ledgeDetectOffset.x * direction, ledgeDetectOffset.y),
            Vector2.down, _ledgeDetectDistance, _ledgeMask);

        if (hit.collider && Physics2D.Raycast(
                _rigidbody.position + new Vector2(ledgeDetectOffset.x * direction, ledgeDetectOffset.y),
                Vector2.up, _ledgeDetectDistance, _ledgeMask) == false)
        {
            isLedgeDetected = hit.collider;
            ledgePoint = hit.point;
        }
        else
        {
            isLedgeDetected = false;
        }
    }

    private void DetectWall()
    {
        RaycastHit2D hit = Physics2D.Raycast(_rigidbody.position + Vector2.up * _wallTopDetectHeight,
            Vector2.right * _direction,
            _wallDetectDistance,
            _wallMask);

        if (hit.collider && Physics2D.Raycast(_rigidbody.position + Vector2.up * _wallBottomDetectHeight,
                Vector2.right * _direction,
                _wallDetectDistance,
                _wallMask).collider)
        {
            isWallDetected = true;
        }
        else
        {
            isWallDetected = false;
        }
    }

    protected virtual void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + (Vector3)_groundDetectCenter, _groundDetectSize);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * _ladderUpDetectOffset, _ladderDetectRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * _ladderDownDetectOffset, _ladderDetectRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position + (Vector3)ledgeDetectOffset,
            transform.position + (Vector3)ledgeDetectOffset + Vector3.down * _ledgeDetectDistance);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position + Vector3.up * _wallTopDetectHeight,
            transform.position +Vector3.up * _wallTopDetectHeight + Vector3.right * _direction * _wallDetectDistance);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position + Vector3.up * _wallBottomDetectHeight,
            transform.position +Vector3.up * _wallBottomDetectHeight + Vector3.right * _direction * _wallDetectDistance);
    }
}