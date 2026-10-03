using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{

    public enum MouseState
    {
        Down = 0,
        Stay,
        Move,
        Press,
        PressAndStay,
        PressAndMove,
        Up
    }

    public enum TargetEquip
    {
        None = 0,
        SlidePull,
        SlidePower,
        TableBoard,
        DetailTurn,
        Etc
    }

    public class InputOutput : MonoBehaviour
    {

        [SerializeField] private Camera _usedCamera;
        public static Camera usedCamera
        {
            get;
            set;
        }

        private static Vector3 _mouseScreenMidPosition;
        private static Vector3 _mouseScreenMaxPosition;

        private static Vector3 _mouseScreenSpeed;
        private static Vector3 _mouseScreenPosition;
        private static Vector3 _mouseWorldSpeed;
        private static Vector3 _mouseWorldPosition;
        private static Ray _mouseWorldRay;


        private static Vector3 _mouseViewportPoint;

        public delegate void OnMouse(MouseState mouseState);
        public static event OnMouse OnMouseState;

        public static Vector3 mouseViewportPoint
        {
            get { return _mouseViewportPoint; }
        }

        public static Vector3 mouseScreenSpeed
        {
            get { return _mouseScreenSpeed; }
        }

        public static Vector3 mouseWorldSpeed
        {
            get { return _mouseWorldSpeed; }
        }

        public static Vector3 mouseWorldPosition
        {
            get { return _mouseWorldPosition; }
        }

        public static Vector3 WorldToScreenPoint(Vector3 position)
        {
            return usedCamera.WorldToScreenPoint(position);
        }

        public static Vector3 mouseScreenMidPosition
        {
            get { return _mouseScreenMidPosition; }
        }
        public static Vector3 mouseScreenMaxPosition
        {
            get { return _mouseScreenMaxPosition; }
        }


        public static TargetEquip targetEquip
        {
            get
            {
                TargetEquip _targetEquip = TargetEquip.None;
                Vector3 inputMousePosition = Input.mousePosition;
                Vector3 viewportPoint = usedCamera.ScreenToViewportPoint(inputMousePosition);

                if (0.0f < viewportPoint.x && viewportPoint.x < 0.07f && 0.64f < viewportPoint.y && viewportPoint.y < 0.84f)
                {
                    _targetEquip = TargetEquip.SlidePull;
                }
                else if (0.0f < viewportPoint.x && viewportPoint.x < 0.07f && 0.0f < viewportPoint.y && viewportPoint.y < 0.64f)
                {
                    _targetEquip = TargetEquip.SlidePower;
                }
                else if (0.07f < viewportPoint.x && viewportPoint.x < 0.93f && 0.0f < viewportPoint.y && viewportPoint.y < 0.84f)
                {
                    _targetEquip = TargetEquip.TableBoard;
                }
                else if (0.93f < viewportPoint.x && viewportPoint.x < 1.00f && 0.0f < viewportPoint.y && viewportPoint.y < 0.60f)
                {
                    _targetEquip = TargetEquip.DetailTurn;
                }
                else
                {
                    _targetEquip = TargetEquip.Etc;
                }

                return _targetEquip;
            }
        }

        private void Awake()
        {
            Debug.Log($"{ this.GetType().Name}  ===== ");
            if (_usedCamera) usedCamera = _usedCamera;

        }

        private void OnEnable()
        {
            // Restore the static reference on enable as well (for example after a script reload).
            if (_usedCamera) usedCamera = _usedCamera;
            if (!usedCamera) usedCamera = Camera.main;

            if (!usedCamera)
            {
                Debug.LogError("InputOutput requires a camera. Assign Used Camera or enable a camera tagged MainCamera.", this);
                enabled = false;
                return;
            }

            Rect rect = usedCamera.rect;
            _mouseScreenMidPosition = usedCamera.ViewportToScreenPoint(rect.center);
            _mouseScreenMaxPosition = usedCamera.ViewportToScreenPoint(rect.max);

            //Debug.Log($"_mouseScreenMidPosition : {_mouseScreenMidPosition}, _mouseScreenMaxPosition : {_mouseScreenMaxPosition}");

        }


        // A release can arrive with the last movement, without a held frame in between.
        // Deliver that movement before Up so power/follow-through use the final position.
        static bool SampleMovement(Vector3 screenPosition, float deltaTime)
        {
            var worldPosition = usedCamera.ScreenToWorldPoint(screenPosition);
            worldPosition.y = 0f;
            var delta = screenPosition - _mouseScreenPosition;
            _mouseViewportPoint = usedCamera.ScreenToViewportPoint(screenPosition);
            _mouseScreenSpeed = delta / Mathf.Max(deltaTime, .000001f);
            _mouseWorldSpeed = (worldPosition - _mouseWorldPosition) / Mathf.Max(deltaTime, .000001f);
            _mouseScreenPosition = screenPosition;
            _mouseWorldPosition = worldPosition;
            return delta.sqrMagnitude > 0;
        }

        static void ReleasePointer(Vector3 screenPosition, float deltaTime)
        {
            if (SampleMovement(screenPosition, deltaTime))
                OnMouseState?.Invoke(MouseState.PressAndMove);
            _mouseScreenSpeed = Vector3.zero;
            _mouseWorldSpeed = Vector3.zero;
            OnMouseState?.Invoke(MouseState.Up);
        }

        private void Update()
        {
            if (!usedCamera) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (Assets.Scripts.Often.PracticeMatchTestRoute.Enabled)
                    Debug.Log($"[PointerSample] down frame={Time.frameCount} position={Input.mousePosition} up={Input.GetMouseButtonUp(0)}");
                //Debug.Log(isUseCueMove);

                Vector3 inputMousePosition = Input.mousePosition;
                Vector3 viewportPoint = usedCamera.ScreenToViewportPoint(inputMousePosition);
                _mouseViewportPoint = viewportPoint;

                _mouseScreenPosition = Input.mousePosition;
                _mouseScreenSpeed = Vector3.zero;
                _mouseWorldSpeed = Vector3.zero;
                _mouseWorldPosition = usedCamera.ScreenToWorldPoint(_mouseScreenPosition);
                _mouseWorldPosition.y = 0f;                             ///// 

                //usedCamera.WorldToViewportPoint();

                //Debug.Log($"DOWN z : {_mouseWordPosition.z}");    
                OnMouseState?.Invoke(MouseState.Down);
            }

            bool isPressed = Input.GetMouseButton(0);
            if (isPressed)
            {
                Vector3 inputMousePosition = Input.mousePosition;
                Vector3 viewportPoint = usedCamera.ScreenToViewportPoint(inputMousePosition);
                Vector3 inputWordPosition = usedCamera.ScreenToWorldPoint(inputMousePosition);

                _mouseViewportPoint = viewportPoint;
                _mouseScreenSpeed = (inputMousePosition - _mouseScreenPosition) / Time.deltaTime;
                _mouseScreenPosition = inputMousePosition;

                //Debug.Log($"PRESS z : {inputWordPosition.z}");

                _mouseWorldSpeed = (inputWordPosition - _mouseWorldPosition) / Time.deltaTime;
                _mouseWorldPosition = inputWordPosition;
                _mouseWorldPosition.y = 0f;



                if (OnMouseState != null)
                {
                    if (_mouseScreenSpeed.magnitude == 0.0f)
                    {
                        OnMouseState(MouseState.PressAndStay);
                    }
                    else
                    {
                        OnMouseState(MouseState.PressAndMove);
                    }
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (Assets.Scripts.Often.PracticeMatchTestRoute.Enabled)
                    Debug.Log($"[PointerSample] up frame={Time.frameCount} position={Input.mousePosition} previous={_mouseScreenPosition}");
                ReleasePointer(Input.mousePosition, Time.deltaTime);
            }

        }

    }
}
