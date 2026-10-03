using System;
using UnityEngine;

namespace Assets.Scripts.Sight.Vital.Pavilion
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

        [Serializable]
        public class TouchArea
        {
            public string kind;
            public float x0;
            public float x_tl;
            public float x_tr;
            public float x_ol;
            public float x_or;
            public float y0;
            public float y1;
            public float y2;
            public float y_dr;
        }

        [SerializeField] TouchArea[] touchAreas;
        public static TouchArea _touchArea;

        [SerializeField] LineRenderer lineX_TL;
        [SerializeField] LineRenderer lineX_TR;
        [SerializeField] LineRenderer lineX_OL;
        [SerializeField] LineRenderer lineX_OR;
        [SerializeField] LineRenderer lineY1;
        [SerializeField] LineRenderer lineY2;
        [SerializeField] Transform cube0;
        [SerializeField] Transform cube1;
        [SerializeField] Transform cube2;
        [SerializeField] Transform cube3;


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

                if (_touchArea.x_ol < viewportPoint.x && viewportPoint.x < _touchArea.x_tl && _touchArea.y1 < viewportPoint.y && viewportPoint.y < _touchArea.y2)
                {
                    _targetEquip = TargetEquip.SlidePull;
                }
                else if (_touchArea.x_ol < viewportPoint.x && viewportPoint.x < _touchArea.x_tl && 0.0f < viewportPoint.y && viewportPoint.y < _touchArea.y1)
                {
                    _targetEquip = TargetEquip.SlidePower;
                }
                else if (_touchArea.x_tl < viewportPoint.x && viewportPoint.x < _touchArea.x_tr && 0.0f < viewportPoint.y && viewportPoint.y < _touchArea.y2)
                {
                    _targetEquip = TargetEquip.TableBoard;
                }
                else if (_touchArea.x_tr < viewportPoint.x && viewportPoint.x < _touchArea.x_or && 0.0f < viewportPoint.y && viewportPoint.y < _touchArea.y_dr)
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
            Debug.Log($"{ this.GetType().Name } ===== ");

            if (_usedCamera) usedCamera = _usedCamera;



            float screenWidth = (float)Screen.width;
            float screenHeight = (float)Screen.height;
            float ratio = screenWidth / screenHeight;

            _touchArea = touchAreas[0];

            //if (1.7 <= ratio && ratio < 1.9)
            //{
            //    _touchArea = touchAreas[0];
            //}
            //else if (1.9 <= ratio && ratio < 2.1)
            //{
            //    _touchArea = touchAreas[1];
            //}
            //else if (2.1 <= ratio && ratio < 2.3)
            //{
            //    _touchArea = touchAreas[2];
            //}
            //else if (1.5 <= ratio && ratio < 1.7)
            //{
            //    _usedCamera.orthographicSize = 1.1f;
            //    _touchArea = touchAreas[3];
            //}

            bool isDrawTouchArea = false;

            if (isDrawTouchArea)
            {

                Debug.Log(_touchArea.x_tl);
                Vector3 viewVec0 = new Vector3(0, 0, 0);
                Vector3 viewVec_tl = new Vector3(_touchArea.x_tl, _touchArea.y1, 0);
                Vector3 viewVec_tr = new Vector3(_touchArea.x_tr, _touchArea.y2, 0);
                Vector3 viewVec3 = new Vector3(1, 1, 0);
                Vector3 viewVec_ol = new Vector3(_touchArea.x_ol, _touchArea.y2, 0);
                Vector3 viewVec_or = new Vector3(_touchArea.x_or, _touchArea.y_dr, 0);

                Vector3 worldVec0 = usedCamera.ViewportToWorldPoint(viewVec0);
                Vector3 worldVec_tl = usedCamera.ViewportToWorldPoint(viewVec_tl);
                Vector3 worldVec_tr = usedCamera.ViewportToWorldPoint(viewVec_tr);
                Vector3 worldVec3 = usedCamera.ViewportToWorldPoint(viewVec3);
                Vector3 worldVec_ol = usedCamera.ViewportToWorldPoint(viewVec_ol);
                Vector3 worldVec_or = usedCamera.ViewportToWorldPoint(viewVec_or);

                worldVec0.y = 0.1f;
                worldVec_tl.y = 0.1f;
                worldVec_tr.y = 0.1f;
                worldVec_ol.y = 0.1f;
                worldVec_or.y = 0.1f;
                worldVec3.y = 0.1f;
                cube0.position = worldVec0;
                cube1.position = worldVec_tl;
                cube2.position = worldVec_tr;
                cube3.position = worldVec3;

                lineX_TL.positionCount = 2;
                lineX_TL.SetPosition(0, new Vector3(worldVec_tl.x, 0.1f, worldVec0.z));
                lineX_TL.SetPosition(1, new Vector3(worldVec_tl.x, 0.1f, worldVec_tr.z));
                lineX_TR.positionCount = 2;
                lineX_TR.SetPosition(0, new Vector3(worldVec_tr.x, 0.1f, worldVec0.z));
                lineX_TR.SetPosition(1, new Vector3(worldVec_tr.x, 0.1f, worldVec_tr.z));
                lineX_OL.positionCount = 2;
                lineX_OL.SetPosition(0, new Vector3(worldVec_ol.x, 0.1f, worldVec0.z));
                lineX_OL.SetPosition(1, new Vector3(worldVec_ol.x, 0.1f, worldVec_ol.z));
                lineX_OR.positionCount = 2;
                lineX_OR.SetPosition(0, new Vector3(worldVec_or.x, 0.1f, worldVec0.z));
                lineX_OR.SetPosition(1, new Vector3(worldVec_or.x, 0.1f, worldVec_or.z));

                lineY1.positionCount = 2;
                lineY1.SetPosition(0, new Vector3(worldVec0.x, 0.1f, worldVec_tl.z));
                lineY1.SetPosition(1, new Vector3(worldVec_tl.x, 0.1f, worldVec_tl.z));
                lineY2.positionCount = 2;
                lineY2.SetPosition(0, new Vector3(worldVec0.x, 0.1f, worldVec_tr.z));
                lineY2.SetPosition(1, new Vector3(worldVec3.x, 0.1f, worldVec_tr.z));

            }





            //Debug.Log($"{ this.GetType().Name} ... ratio : {ratio} ===== viewVec1 : {viewVec_tl},  viewVec2 : {viewVec_tr}");
            //Debug.Log($"{ this.GetType().Name} ... ratio : {ratio} ===== worldVec1 : {worldVec_tl},  worldVec2 : {worldVec_tr}");


            //Rect rect = _usedCamera.rect;
            //float scaleheight = ((float)Screen.width / Screen.height) / ((float)16 / 9);        // (가로 / 세로)
            //float scalewidth = 1f / scaleheight;
            //if (scaleheight < 1)
            //{
            //    rect.height = scaleheight;
            //    rect.y = (1f - scaleheight) / 2f;
            //}
            //else
            //{
            //    rect.width = scalewidth;
            //    rect.x = (1f - scalewidth) / 2f;
            //}
            //_usedCamera.rect = rect;

        }

        private void OnEnable()
        {
            Rect rect = usedCamera.rect;
            _mouseScreenMidPosition = usedCamera.ViewportToScreenPoint(rect.center);
            _mouseScreenMaxPosition = usedCamera.ViewportToScreenPoint(rect.max);

            //Debug.Log($"_mouseScreenMidPosition : {_mouseScreenMidPosition}, _mouseScreenMaxPosition : {_mouseScreenMaxPosition}");

        }


        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
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
                _mouseWorldPosition = usedCamera.ScreenToWorldPoint(_mouseScreenPosition);
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
                Vector3 inputMousePosition = Input.mousePosition;
                Vector3 worldPosition = usedCamera.ScreenToWorldPoint(inputMousePosition);
                worldPosition.y = 0;
                Vector3 delta = inputMousePosition - _mouseScreenPosition;
                _mouseViewportPoint = usedCamera.ScreenToViewportPoint(inputMousePosition);
                _mouseScreenSpeed = delta / Mathf.Max(Time.deltaTime, .000001f);
                _mouseWorldSpeed = (worldPosition - _mouseWorldPosition) / Mathf.Max(Time.deltaTime, .000001f);
                _mouseScreenPosition = inputMousePosition;
                _mouseWorldPosition = worldPosition;
                if (delta.sqrMagnitude > 0) OnMouseState?.Invoke(MouseState.PressAndMove);
                _mouseScreenSpeed = Vector3.zero;
                _mouseWorldSpeed = Vector3.zero;

                OnMouseState?.Invoke(MouseState.Up);
            }

        }


    }
}
