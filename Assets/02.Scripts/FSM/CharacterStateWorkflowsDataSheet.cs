using System.Collections.Generic;
using UnityEngine;

public static class CharacterStateWorkflowsDataSheet
{
    public abstract class WorkflowBase : IWorkflow<State>
    {
        public abstract State ID { get; }
        public virtual bool CanExecute => true;

        public int Current => current;
        protected int current;

        protected CharacterMachine machine;
        protected Transform transform;
        protected Rigidbody2D rigidbody;
        protected CapsuleCollider2D[] colliders;
        protected Animator animator;
        
        protected bool hasFixedUpdatedAtVeryFirst;

        public WorkflowBase(CharacterMachine machine)
        {
            this.machine = machine;
            this.transform = machine.transform;
            this.animator = machine.animator;
            this.rigidbody = machine.GetComponent<Rigidbody2D>();
            this.colliders = machine.GetComponentsInChildren<CapsuleCollider2D>();
        }

        public virtual State OnUpdate()
        {
            return hasFixedUpdatedAtVeryFirst ? ID : State.None;
        }

        public virtual void OnFixedUpdate()
        {
            if (hasFixedUpdatedAtVeryFirst == false)
                hasFixedUpdatedAtVeryFirst = true;
        }

        public void Reset()
        {
            current = 0;
        }

        public virtual void OnEnter(object[] parameters)
        {
            hasFixedUpdatedAtVeryFirst = false;
            Reset();
        }

        public virtual void OnExit()
        {
        }
    }

    public class Idle : WorkflowBase
    {
        public override State ID => State.Idle;

        public Idle(CharacterMachine machine) : base(machine)
        {
        }

        public override void OnEnter(object[] parameters)
        {
            base.OnEnter(parameters);
            machine.hasJumped = false;
            machine.hasSecondJumped = false;
            machine.isDirectionChangeable = true;
            machine.isMovable = true;
            animator.Play("Idle");
        }

        public override State OnUpdate()
        {
            State next = base.OnUpdate();

            if (next == State.None)
                return ID;

            switch (current)
            {
                default:
                {
                    if (Mathf.Abs(machine.horizontal) > 0.0f)
                    {
                        next = State.Move;
                    }

                    if (machine.isGrounded == false)
                    {
                        next = State.Fall;
                    }
                }
                    break;
            }

            return next;
        }
    }

    public class Move : WorkflowBase
    {
        public override State ID => State.Move;

        public Move(CharacterMachine machine) : base(machine)
        {
        }

        public override void OnEnter(object[] parameters)
        {
            base.OnEnter(parameters);
            machine.isDirectionChangeable = true;
            machine.isMovable = true;
            animator.Play("Move");
        }

        public override State OnUpdate()
        {
            State next = base.OnUpdate();

            if (next == State.None)
                return ID;

            switch (current)
            {
                default:
                {
                    if (machine.horizontal == 0.0f)
                    {
                        next = State.Idle;
                    }

                    if (machine.isGrounded == false)
                    {
                        next = State.Fall;
                    }
                }
                    break;
            }

            return next;
        }
    }

    public class Jump : WorkflowBase
    {
        public override State ID => State.Jump;

        public override bool CanExecute => base.CanExecute &&
                                           machine.hasJumped == false &&
                                           (((machine.current == State.Idle || machine.current == State.Move) &&
                                             machine.isGrounded) ||
                                            machine.current == State.LadderClimbing || machine.current == State.Ledge ||
                                            machine.current == State.WallSlide);

        private float _force;

        public Jump(CharacterMachine machine, float force) : base(machine)
        {
            _force = force;
        }

        public override void OnEnter(object[] parameters)
        {
            base.OnEnter(parameters);
            machine.hasJumped = true;
            machine.isDirectionChangeable = true;
            machine.isMovable = false;
            rigidbody.velocity = machine.previous == State.LadderClimbing || machine.previous == State.Ledge || machine.previous == State.WallSlide
                ? new Vector2(machine.horizontal * machine.speed, 0.0f)
                : new Vector2(rigidbody.velocity.x, 0.0f);
            rigidbody.AddForce(Vector2.up * _force, ForceMode2D.Impulse);
            animator.Play("Jump");
        }

        public override State OnUpdate()
        {
            State next = base.OnUpdate();

            if (next == State.None)
                return ID;
            switch (current)
            {
                default:
                {
                    if (rigidbody.velocity.y <= 0.0f)
                    {
                        next = machine.isGrounded ? State.Idle : State.Fall;
                    }
                }
                    break;
            }

            return next;
        }
    }

    public class SecondJump : WorkflowBase
    {
        public override State ID => State.SecondJump;

        public override bool CanExecute => base.CanExecute &&
                                           machine.hasSecondJumped == false &&
                                           (machine.current == State.Jump || machine.current == State.Fall) &&
                                           machine.isGrounded == false;

        private float _force;

        public SecondJump(CharacterMachine machine, float force) : base(machine)
        {
            _force = force;
        }

        public override void OnEnter(object[] parameters)
        {
            base.OnEnter(parameters);
            machine.hasSecondJumped = true;
            machine.isDirectionChangeable = true;
            machine.isMovable = false;
            machine.move = Vector2.zero;
            rigidbody.velocity = machine.horizontal == 0
                ? new Vector2(rigidbody.velocity.x, 0.0f)
                : new Vector2(machine.horizontal * machine.speed, 0.0f);
            rigidbody.AddForce(Vector2.up * _force, ForceMode2D.Impulse);
            animator.Play("SecondJump");
        }

        public override State OnUpdate()
        {
            State next = base.OnUpdate();

            if (next == State.None)
                return ID;
            
            switch (current)
            {
                default:
                {
                    if (rigidbody.velocity.y <= 0.0f)
                    {
                        next = machine.isGrounded ? State.Idle : State.Fall;
                    }
                }
                    break;
            }

            return next;
        }
    }

    public class Fall : WorkflowBase
    {
        public override State ID => State.Fall;
        private float _landingDistance;
        private float _startPosY;

        public Fall(CharacterMachine machine, float landingDistance) : base(machine)
        {
            _landingDistance = landingDistance;
        }

        public override void OnEnter(object[] parameters)
        {
            base.OnEnter(parameters);
            machine.isDirectionChangeable = true;
            machine.isMovable = false;
            _startPosY = rigidbody.position.y;
            animator.Play("Fall");
        }


        public override State OnUpdate()
        {
            State next = base.OnUpdate();

            if (next == State.None)
                return ID;
            
            switch (current)
            {
                default:
                {
                    if (machine.isGrounded)
                    {
                        next = (_startPosY - rigidbody.position.y) < _landingDistance ? State.Idle : State.Land;
                    }
                }
                    break;
            }

            return next;
        }
    }

    public class Land : WorkflowBase
    {
        public override State ID => State.Land;

        public Land(CharacterMachine machine) : base(machine)
        {
        }

        public override void OnEnter(object[] parameters)
        {
            base.OnEnter(parameters);
            machine.isDirectionChangeable = true;
            machine.isMovable = false;
            machine.move = Vector2.zero;
            rigidbody.velocity = Vector2.zero;
            animator.Play("Land");
        }

        public override State OnUpdate()
        {
            State next = base.OnUpdate();

            if (next == State.None)
                return ID;
            
            switch (current)
            {
                default:
                {
                    if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >=
                        1.0f) // 현재 애니메이터의 재생 중인 상태의 정보에서 일반화된 시간이 1.0 이상이면
                    {
                        next = State.Idle;
                    }
                }
                    break;
            }

            return next;
        }
    }

    public class Crouch : WorkflowBase
    {
        public override State ID => State.Crouch;

        public override bool CanExecute => base.CanExecute &&
                                           (machine.current == State.Idle || machine.current == State.Move) &&
                                           machine.isGrounded;

        private Vector2 _offsetCrouched;
        private Vector2 _sizeCrouched;
        private Vector2 _offsetOrigin;
        private Vector2 _sizeOrigin;


        public Crouch(CharacterMachine machine, Vector2 offsetCrouched, Vector2 sizeCrouched) : base(machine)
        {
            _offsetCrouched = offsetCrouched;
            _sizeCrouched = sizeCrouched;

            _offsetOrigin = colliders[0].offset;
            _sizeOrigin = colliders[0].size;
        }

        public override void OnEnter(object[] parameters)
        {
            base.OnEnter(parameters);
            machine.isDirectionChangeable = true;
            machine.isMovable = false;
            machine.move = Vector2.zero;
            rigidbody.velocity = Vector2.zero;

            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].offset = _offsetCrouched;
                colliders[i].size = _sizeCrouched;
            }

            animator.Play("Crouch");
        }

        public override void OnExit()
        {
            base.OnExit();

            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].offset = _offsetOrigin;
                colliders[i].size = _sizeOrigin;
            }
        }

        public override State OnUpdate()
        {
            State next = base.OnUpdate();

            if (next == State.None)
                return ID;
            
            switch (current)
            {
                case 0:
                {
                    if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >=
                        1.0f) // 현재 애니메이터의 재생 중인 상태의 정보에서 일반화된 시간이 1.0 이상이면
                    {
                        animator.Play("CrouchIdle");
                        current++;
                    }
                }
                    break;
                default:
                {
                    if (machine.isGrounded == false)
                    {
                        next = State.Fall;
                    }
                }
                    break;
            }

            return next;
        }
    }

    public class LadderClimbing : WorkflowBase
    {
        public override State ID => State.LadderClimbing;

        public override bool CanExecute => base.CanExecute &&
                                           (machine.current == State.Idle || machine.current == State.Move ||
                                            machine.current == State.Jump ||
                                            machine.current == State.Fall || machine.current == State.SecondJump);

        private Ladder _ladder;
        private float _climbingSpeed;
        private float _vertical;

        public LadderClimbing(CharacterMachine machine, float climbingSpeed) : base(machine)
        {
            _climbingSpeed = climbingSpeed;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parameters"> 0 : (Ladder), 1 : 위/아래(int) </param>
        public override void OnEnter(object[] parameters)
        {
            base.OnEnter(parameters);
            machine.isDirectionChangeable = false;
            machine.isMovable = false;
            machine.hasJumped = false;
            machine.hasSecondJumped = false;
            machine.move = Vector2.zero;
            rigidbody.velocity = Vector2.zero;
            rigidbody.bodyType = RigidbodyType2D.Kinematic;

            animator.Play("LadderClimbing");
            animator.speed = 0.0f;

            _ladder = (Ladder)parameters[0];
            int toward = (int)parameters[1];

            if (toward > 0)
            {
                transform.position = transform.position.y > _ladder.upStartPos.y
                    ? new Vector3(_ladder.upStartPos.x, transform.position.y)
                    : _ladder.upStartPos;
            }
            else if (toward < 0)
            {
                transform.position = transform.position.y < _ladder.downStartPos.y
                    ? new Vector3(_ladder.downStartPos.x, transform.position.y)
                    : _ladder.downStartPos;
            }
            else
            {
                throw new System.Exception("[LadderClimbing] Invalid direction value: " + toward);
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            rigidbody.bodyType = RigidbodyType2D.Dynamic;
            animator.speed = 1.0f;
        }

        public override State OnUpdate()
        {
            State next = base.OnUpdate();

            if (next == State.None)
                return ID;
            
            switch (current)
            {
                case 0:
                {
                    // nothing to do
                }
                    break;
                default:
                {
                    if (machine.isGrounded)
                    {
                        next = State.Idle;
                    }
                    else if (transform.position.y > _ladder.upEndPos.y)
                    {
                        transform.position = _ladder.top;
                        next = State.Idle;
                    }
                    else if (transform.position.y < _ladder.downEndPos.y)
                    {
                        next = State.Idle;
                    }
                    else
                    {
                        _vertical = machine.vertical;
                    }
                }
                    break;
            }

            return next;
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();

            switch (current)
            {
                case 0:
                {
                    current++;
                }
                    break;
                default:
                {
                    if (machine.isGrounded)
                    {
                        // nothing to do
                    }
                    else if (transform.position.y > _ladder.upEndPos.y)
                    {
                        // nothing to do
                    }
                    else if (transform.position.y < _ladder.downEndPos.y)
                    {
                        // nothing to do
                    }
                    else
                    {
                        animator.speed = Mathf.Abs(_vertical);
                        transform.position += Vector3.up * _vertical * _climbingSpeed * Time.fixedDeltaTime;
                    }
                }
                    break;
            }
        }
    }

    public class Ledge : WorkflowBase
    {
        public override State ID => State.Ledge;

        public override bool CanExecute => base.CanExecute &&
                                           machine.isLedgeDetected &&
                                           (machine.current == State.Jump || machine.current == State.SecondJump ||
                                            machine.current == State.Fall);

        public Ledge(CharacterMachine machine) : base(machine)
        {
        }

        public override void OnEnter(object[] parameters)
        {
            base.OnEnter(parameters);
            machine.isDirectionChangeable = false;
            machine.isMovable = false;
            machine.hasJumped = false;
            machine.hasSecondJumped = false;
            machine.move = Vector2.zero;
            rigidbody.velocity = Vector2.zero;
            rigidbody.bodyType = RigidbodyType2D.Kinematic;
            transform.position = machine.ledgePoint - new Vector2(machine.ledgeDetectOffset.x * machine.direction,
                machine.ledgeDetectOffset.y);
            animator.Play("LedgeStart");
        }

        public override void OnExit()
        {
            base.OnExit();
            rigidbody.bodyType = RigidbodyType2D.Dynamic;
        }

        public override State OnUpdate()
        {
            State next = base.OnUpdate();

            if (next == State.None)
                return ID;
            
            switch (current)
            {
                case 0:
                {
                    if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1.0f)
                    {
                        current++;
                    }
                }
                    break;
                case 1:
                {
                    animator.Play("LedgeIdle");
                    current++;
                }
                    break;
                default:
                {
                    // nothing to do
                }
                    break;
            }

            return next;
        }
    }

    public class LedgeClimb : WorkflowBase
    {
        public override State ID => State.LedgeClimb;

        public override bool CanExecute => base.CanExecute &&
                                           machine.current == State.Ledge;

        private Vector2 _different; // btw ledge & transform
        private Vector2 _differentRatio;
        private Vector2 _startPos;
        private float _clipLength;
        private float _timer;

        public LedgeClimb(CharacterMachine machine) : base(machine)
        {
        }

        public override void OnEnter(object[] parameters)
        {
            base.OnEnter(parameters);
            machine.isDirectionChangeable = false;
            machine.isMovable = false;
            machine.move = Vector2.zero;
            rigidbody.velocity = Vector2.zero;
            rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _different = machine.ledgePoint - (Vector2)transform.position;

            Vector2 distance = new Vector2(Mathf.Abs(_different.x), Mathf.Abs(_different.y));
            
            _differentRatio = new Vector2(distance.y / (distance.x + distance.y),
                distance.x / (distance.x + distance.y));
            _startPos = transform.position;
            animator.Play("LedgeClimb");
            _clipLength = animator.GetCurrentAnimatorClipInfo(0).Length;
            _timer = 0.0f;
        }

        public override void OnExit()
        {
            base.OnExit();
            rigidbody.bodyType = RigidbodyType2D.Dynamic;
        }

        public override State OnUpdate()
        {
            State next = base.OnUpdate();

            if (next == State.None)
                return ID;

            switch (current)
            {
                case 0:
                {
                    transform.position = Vector2.Lerp(_startPos,
                        _startPos + Vector2.up * _different.y,
                        _timer / (_clipLength * _differentRatio.x));

                    if (_timer >= _clipLength * _differentRatio.x)
                    {
                        _startPos = transform.position;
                        _timer = 0.0f;
                        current++;
                    }
                }
                    break;
                case 1:
                {
                    transform.position = Vector2.Lerp(_startPos,
                                                         _startPos + Vector2.right * _different.x,
                                                         _timer / (_clipLength * _differentRatio.y));

                    if (_timer >= _clipLength * _differentRatio.y)
                    {
                        current++;
                    }
                }
                    break;

                default:
                {
                    next = State.Idle;
                }
                    break;
            }

            _timer += Time.deltaTime * 3.0f;
            return next;
        }
    }
    
     public class WallSlide : WorkflowBase
    {
        public override State ID => State.WallSlide;

        public override bool CanExecute => base.CanExecute &&
                                           machine.isWallDetected &&
                                           (machine.current == State.Fall);

        private float _dampingFactor;
        private Vector2 _velocity;

        public WallSlide(CharacterMachine machine, float dampingFactor) : base(machine)
        {
            _dampingFactor = dampingFactor;
        }

        public override void OnEnter(object[] parameters)
        {
            base.OnEnter(parameters);
            machine.isDirectionChangeable = false;
            machine.isMovable = false;
            machine.hasJumped = false;
            machine.hasSecondJumped = false;
            machine.move = Vector2.zero;
            rigidbody.velocity = Vector2.zero;
            rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _velocity = Vector2.zero;
            animator.Play("WallSlide");
        }

        public override void OnExit()
        {
            base.OnExit();
            rigidbody.bodyType = RigidbodyType2D.Dynamic;
        }

        public override State OnUpdate()
        {
            State next = base.OnUpdate();

            if (next == State.None)
                return ID;

            switch (current)
            {
                default:
                {
                    if (machine.isWallDetected == false)
                    {
                        next = State.Idle;
                    }
                }
                    break;
            }

            return next;
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            _velocity += Physics2D.gravity * (1.0f - _dampingFactor) * Time.fixedDeltaTime;
            transform.position += (Vector3)_velocity * Time.fixedDeltaTime;
        }
    }
     
    public class Attack : WorkflowBase
    {
        public override State ID => State.Attack;

        public override bool CanExecute
        {
            get
            {
                if (_combo > 0 &&
                    Time.time - _exitTimeMark >= _comboResetTime)
                {
                    _combo = 0;
                    _hasHit = false;
                }

                if (_combo > _comboMax)
                {
                    return false;
                }
                
                if (base.CanExecute &&
                    _combo <= _comboMax && 
                    (_combo == 0) || (_combo > 0 && _hasHit) &&
                    machine.current == State.Idle ||
                    machine.current == State.Move || 
                    machine.current == State.Jump ||
                    machine.current == State.SecondJump ||
                    machine.current == State.Fall )
                {
                    return true;
                }

                return false;
            }
        } 

        private int _comboMax;
        private int _combo;
        private float _comboResetTime;
        private float _exitTimeMark;
        private bool _hasHit;
        private AnimatorEvents _animatorEvents;

        public class AttackSetting
        {
            public Vector2 center;
            public Vector2 size;
            public float distance;
            public LayerMask targetMask;
            public int targetMax;
            public float damageGain;

            public AttackSetting(Vector2 center, Vector2 size, float distance, LayerMask targetMask, int targetMax, float damageGain)
            {
                this.center = center;
                this.size = size;
                this.distance = distance;
                this.targetMask = targetMask;
                this.targetMax = targetMax;
                this.damageGain = damageGain;
            }
        }
        private AttackSetting[] _attackSettings;
        private List<CharacterMachine> _targets = new List<CharacterMachine>();
        

        public Attack(CharacterMachine machine, int comboMax, float comboResetTime, AttackSetting[] attackSettings) : base(machine)
        {
            _comboMax = comboMax;
            _comboResetTime = comboResetTime;
            _animatorEvents = machine.GetComponentInChildren<AnimatorEvents>();
            _attackSettings = attackSettings;
            
            _animatorEvents.onAttackHit += () =>
            {
                foreach (var target in _targets)
                {
                    if (target == null)
                        continue;
                    
                    float damage = Random.Range(machine.attackForceMin, machine.attackForceMax) * _attackSettings[_combo - 1].damageGain;
                    target.DepleteHp(machine, damage);
                    target.Knockback(new Vector2(machine.direction * 2.0f, 0.0f));
                    DamagePopUp.Create(target.transform.position + Vector3.up * 0.5f, (int)damage, machine.gameObject.layer);
                }
                
                _hasHit = true;
            };
            
        }

        public override void OnEnter(object[] parameters)
        {
            base.OnEnter(parameters);
            machine.isDirectionChangeable = false;
            machine.isMovable = false;

            if (machine.isGrounded)
            {
                machine.move = Vector2.zero;
                rigidbody.velocity = Vector2.zero;
            }
            
            AttackSetting setting = _attackSettings[_combo];
                
            RaycastHit2D[] hits = Physics2D.BoxCastAll(
                origin: rigidbody.position + new Vector2(setting.center.x * machine.direction, setting.center.y),
                size: setting.size,
                angle: 0.0f,
                direction: Vector2.right * machine.direction,
                distance: setting.distance,
                layerMask: setting.targetMask);
            
            _targets.Clear();

            for (int i = 0; i < hits.Length; i++)
            {
                if (i >= setting.targetMax)
                    break;

                if (hits[i].collider.TryGetComponent(out CharacterMachine character))
                {
                    _targets.Add(character);

                }
            }
            
            _hasHit = false;
            animator.SetFloat("attackComboStack", _combo++);
            animator.Play("Attack");
        }

        public override void OnExit()
        {
            base.OnExit();
            _exitTimeMark = Time.time;
        }

        public override State OnUpdate()
        {
            State next = base.OnUpdate();

            if (next == State.None)
                return ID;

            switch (current)
            {
                default:
                {
                    if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1.0f)
                    {
                        next = State.Idle;
                    }

                    if (_hasHit)
                    {
                        AttackSetting setting = _attackSettings[_combo - 1];
                        Vector2 center = rigidbody.position + new Vector2(setting.center.x * machine.direction, setting.center.y);
                        Vector2 size = setting.size;
                        float distance = setting.distance;
                        
                        Debug.DrawLine(center + new Vector2(-size.x / 2.0f * machine.direction, size.y / 2.0f),
                                        center + new Vector2(size.x / 2.0f * machine.direction, size.y / 2.0f) + Vector2.right * machine.direction * distance);
                        
                        Debug.DrawLine(center + new Vector2(-size.x / 2.0f * machine.direction, -size.y / 2.0f),
                                        center + new Vector2(size.x / 2.0f * machine.direction, -size.y / 2.0f) + Vector2.right * machine.direction * distance);
                        
                        Debug.DrawLine(center + new Vector2(-size.x / 2.0f * machine.direction, size.y / 2.0f),
                                        center + new Vector2(-size.x / 2.0f * machine.direction, -size.y / 2.0f));
                        
                        Debug.DrawLine(center + new Vector2(size.x / 2.0f * machine.direction, -size.y / 2.0f) + Vector2.right * machine.direction * distance,
                                        center + new Vector2(size.x / 2.0f * machine.direction, size.y / 2.0f) + Vector2.right * machine.direction * distance);
                    }
                }
                    break;
            }

            return next;
        }
    }
    
    public class Hurt : WorkflowBase
    {
        public override State ID => State.Hurt;
        public override bool CanExecute => base.CanExecute &&
                                           (machine.current == State.Idle ||
                                            machine.current == State.Move ||
                                            machine.current == State.Jump ||
                                            machine.current == State.SecondJump ||
                                            machine.current == State.Fall ||
                                            machine.current == State.Land ||
                                            machine.current == State.Crouch ||
                                            machine.current == State.Ledge ||
                                            machine.current == State.LedgeClimb ||
                                            machine.current == State.WallSlide);

        public Hurt(CharacterMachine machine) : base(machine)
        {
        }

        public override void OnEnter(object[] parameters)
        {
            base.OnEnter(parameters);
            machine.isDirectionChangeable = false;
            machine.isMovable = false;
            machine.move = Vector2.zero;
            rigidbody.velocity = Vector2.zero;
            animator.Play("Hurt");
        }

        public override State OnUpdate()
        {
            State next = base.OnUpdate();

            if (next == State.None)
                return ID;

            switch (current)
            {
                default:
                {
                    if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1.0f)
                    {
                        next = State.Idle;
                    }
                }
                    break;
            }

            return next;
        }
    }
    
    public class Die : WorkflowBase
    {
        public override State ID => State.Die;

        public Die(CharacterMachine machine) : base(machine)
        {
        }

        public override void OnEnter(object[] parameters)
        {
            base.OnEnter(parameters);
            machine.isInvincible = true;
            machine.isDirectionChangeable = false;
            machine.isMovable = false;
            machine.move = Vector2.zero;
            rigidbody.velocity = Vector2.zero;
            animator.Play("Die");
        }

        public override State OnUpdate()
        {
            State next = base.OnUpdate();

            if (next == State.None)
                return ID;

            switch (current)
            {
                default:
                {
                    if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1.0f)
                    {
                        GameObject.Destroy(machine.gameObject);
                    }
                }
                    break;
            }

            return next;
        }
    }

    public static IEnumerable<KeyValuePair<State, IWorkflow<State>>> GetWorkflowsForPlayer(CharacterMachine machine)
    {
        return new Dictionary<State, IWorkflow<State>>()
        {
            { State.Idle, new Idle(machine) },
            { State.Move, new Move(machine) },
            { State.Jump, new Jump(machine, 4.2f) },
            { State.SecondJump, new SecondJump(machine, 3.2f) },
            { State.Fall, new Fall(machine, 1.0f) },
            { State.Land, new Land(machine) },
            { State.Crouch, new Crouch(machine, new Vector2(0.0f, 0.06f), new Vector2(0.12f, 0.12f)) },
            { State.LadderClimbing, new LadderClimbing(machine, 1.0f) },
            { State.Ledge, new Ledge(machine) },
            { State.LedgeClimb, new LedgeClimb(machine) },
            { State.WallSlide, new WallSlide(machine, 0.8f) },
            { State.Attack, new Attack(machine, 2, 0.3f, new Attack.AttackSetting[3]
            {
                new Attack.AttackSetting(new Vector2(0.2f, 0.18f), new Vector2(0.5f, 0.6f), 0.0f, 1<<LayerMask.NameToLayer("EnemyTrigger"), 2, 0.8f),
                new Attack.AttackSetting(new Vector2(0.2f, 0.18f), new Vector2(0.5f, 0.6f), 0.0f, 1<<LayerMask.NameToLayer("EnemyTrigger"), 2, 0.95f),
                new Attack.AttackSetting(new Vector2(0.2f, 0.18f), new Vector2(0.5f, 0.6f), 0.2f, 1<<LayerMask.NameToLayer("EnemyTrigger"), 2, 1.3f)
            }) },
            { State.Hurt, new Hurt(machine) },
            { State.Die, new Die(machine) }
        };
    }
    
    public static IEnumerable<KeyValuePair<State, IWorkflow<State>>> GetWorkflowsForEnemy(CharacterMachine machine)
    {
        return new Dictionary<State, IWorkflow<State>>()
        {
            { State.Idle, new Idle(machine) },
            { State.Move, new Move(machine) },
            { State.Jump, new Jump(machine, 4.2f) },
            { State.SecondJump, new SecondJump(machine, 3.2f) },
            { State.Fall, new Fall(machine, 1.0f) },
            { State.Land, new Land(machine) },
            { State.Crouch, new Crouch(machine, new Vector2(0.0f, 0.06f), new Vector2(0.12f, 0.12f)) },
            { State.LadderClimbing, new LadderClimbing(machine, 1.0f) },
            { State.Ledge, new Ledge(machine) },
            { State.LedgeClimb, new LedgeClimb(machine) },
            { State.WallSlide, new WallSlide(machine, 0.8f) },
            { State.Attack, new Attack(machine, 0, 0.0f, null) },
            { State.Hurt, new Hurt(machine) },
            { State.Die, new Die(machine) }
        };
    }
}