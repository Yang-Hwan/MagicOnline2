using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Assets.Scripts.Sight.Surface.Arise;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public class MainSwipe : MonoBehaviour, IDragHandler
    {

		[SerializeField]
		private Scrollbar scrollBar;                    // Scrollbar의 위치를 바탕으로 현재 페이지 검사
		[SerializeField]
		private RectTransform[] circleContents;             // 현재 페이지를 나타내는 원 Image UI들의 Transform
		[SerializeField]
		private float swipeTime = 0.2f;         // 페이지가 Swipe 되는 시간
		[SerializeField]
		private float swipeDistance = 50.0f;        // 페이지가 Swipe되기 위해 움직여야 하는 최소 거리

		private float[] scrollPageValues;           // 각 페이지의 위치 값 [0.0 - 1.0]
		private float valueDistance = 0;            // 각 페이지 사이의 거리
		private int currentPage = 0;            // 현재 페이지
		private int maxPage = 0;                // 최대 페이지
		private float startTouchY;              // 터치 시작 위치
		private float endTouchY;                    // 터치 종료 위치
		private bool isSwipeMode = false;       // 현재 Swipe가 되고 있는지 체크
		private float circleContentScale = 1.3f;  // 현재 페이지의 원 크기(배율)

		int mainP;
		[SerializeField] MainComp[] mainComps;
		[SerializeField] Transform arrUpDn;
		[SerializeField] Sprite nav_bg_off;
		[SerializeField] Sprite nav_bg_on;
		PracticeMenuView practiceMenu;

		private void Awake()
		{
			// 스크롤 되는 페이지의 각 value 값을 저장하는 배열 메모리 할당
			scrollPageValues = new float[transform.childCount];
			mainComps = new MainComp[transform.childCount];
			// 스크롤 되는 페이지 사이의 거리  비율값
			valueDistance = 1f / (scrollPageValues.Length - 1f);

			// 스크롤 되는 페이지의 각 value 위치 설정 [0 <= value <= 1]
			for (int i = 0; i < scrollPageValues.Length; ++i)
			{
				scrollPageValues[i] = valueDistance * i;
				UnityAction act;
				if (i == 0) act = btn0;
				else if (i == 1)
				{
					act = btn1;
				}
				else if (i == 2)
				{
					act = btn2;
				}
				else if (i == 3)
				{
					act = btn3;
				}
				else
				{
					act = btn4;
				}
				//Debug.Log(i);
				circleContents[i].GetComponent<Button>().onClick.AddListener(act);
			}

			// 최대 페이지의 수
			maxPage = transform.childCount;


			for (int i = 0; i < scrollPageValues.Length; ++i)
			{
				mainComps[i] = transform.GetChild(i).GetComponent<MainComp>();
				mainComps[i].SetTxt(4 - i);
			}
			practiceMenu = mainComps[1].gameObject.AddComponent<PracticeMenuView>();

			// 최초 시작할 때 0번 페이지를 볼 수 있도록 설정
			//SetScrollBarValue(4);
	
			//BackendDuty.Instance.ConsistPage(2);
			Debug.Log($"MainSwipe.Awake ... ");
		}

		void OnEnable()
        {
			currentPage = 0;
			mainP = GetInitialPage();
			Debug.Log($"MainSwipe.OnEnable ... mainP : {mainP}" );
			//SetScrollBarValue(mainP);
		}

		void OnDisable()
        {
			Debug.Log($"MainSwipe.OnDisable ... ");
		}

		private void Start()
		{
			//for (int i = 0; i < scrollPageValues.Length; ++i)
			//{
			//	Debug.Log($"MainSwipe Start circleContents[i] : {circleContents[i].name}");
			//}
			// Select the returning page before input is accepted. A delayed
			// initialization could overwrite a user's early menu selection.
			Canvas.ForceUpdateCanvases();
			mainP = GetInitialPage();
			currentPage = mainP;
			scrollBar.value = scrollPageValues[mainP];
			UpdateCircleContent();
			Assets.Scripts.Often.PracticeSceneFlow.ConsumeMenuReturn();
		}

		private int GetInitialPage()
		{
			if (Assets.Scripts.Often.PracticeSceneFlow.ReturnToPracticeMenu) return 3;
			// Arise 씬을 거치지 않은 경우 기본 메뉴(0)를 사용한다.
			var backendDuty = BackendDuty.Instance;
			int menuPage = backendDuty != null ? backendDuty.mainP : 0;
			return Mathf.Clamp(4 - menuPage, 0, Mathf.Max(0, maxPage - 1));
		}

		public void OnDrag(PointerEventData eventData) {}

		private void btn0() => SetScrollBarValue(0);
		private void btn1() => SetScrollBarValue(1);
		private void btn2() => SetScrollBarValue(2);
		// Navigation is stored bottom-to-top: index 3 is the second visible button.
		private void btn3() => SetScrollBarValue(3);
		private void btn4() => SetScrollBarValue(4);

		public void SetScrollBarValue(int index)
		{
			if (index == currentPage) { return; }
			//Debug.Log($"currentPage : {currentPage}, index : {index}, val : {scrollPageValues[index]}");
			currentPage = index;
			//scrollBar.value	= scrollPageValues[index];
			OnSwipeOneStepAsync(currentPage).Forget();
			UpdateCircleContent();
		}

		void Update() => UpdateInput();

		private void UpdateInput()
		{
			// 현재 Swipe를 진행중이면 터치 불가
			if (isSwipeMode == true) return;

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
			// 마우스 왼쪽 버튼을 눌렀을 때 1회
			if (Input.GetMouseButtonDown(0))
			{
				// 터치 시작 지점 (Swipe 방향 구분)
				startTouchY = Input.mousePosition.y;
			}
			else if (Input.GetMouseButtonUp(0))
			{
				// 터치 종료 지점 (Swipe 방향 구분)
				endTouchY = Input.mousePosition.y;

				UpdateSwipe();
			}
#endif

#if UNITY_ANDROID
			if (Input.touchCount == 1)
			{
				Touch touch = Input.GetTouch(0);

				if (touch.phase == TouchPhase.Began)
				{
					// 터치 시작 지점 (Swipe 방향 구분)
					startTouchY = touch.position.y;
				}
				else if (touch.phase == TouchPhase.Ended)
				{
					// 터치 종료 지점 (Swipe 방향 구분)
					endTouchY = touch.position.y;

					UpdateSwipe();
				}
			}
#endif
		}


		public void OnSliderValueChange()
		{
			float val = scrollBar.value;
			if (currentPage == 2)
			{
				val = 0.5f;
				//startTouchY = startTouchY * .1f;
				endTouchY = startTouchY;
			}
			scrollBar.value = val;
			Debug.Log($"currentPage : {currentPage}, val : {val}");

		}


		private void UpdateSwipe()
		{
			//if (currentPage == 2)
			//{

			//}

			// 너무 작은 거리를 움직였을 때는 Swipe X
			if (Mathf.Abs(startTouchY - endTouchY) < swipeDistance)
			{
				// 원래 페이지로 Swipe해서 돌아간다
				//StartCoroutine(OnSwipeOneStep(currentPage));
				OnSwipeOneStepAsync(currentPage).Forget();
				return;
			}

			// Swipe 방향
			bool isLeft = startTouchY < endTouchY ? true : false;



			// 이동 방향이 왼쪽일 때
			if (isLeft)
			{
				// 현재 페이지가 왼쪽 끝이면 종료
				if (currentPage == 0) return;
				// 왼쪽으로 이동을 위해 현재 페이지를 1 감소
				currentPage--;
			}
			// 이동 방향이 오른쪽일 떄
			else
			{
				// 현재 페이지가 오른쪽 끝이면 종료
				if (currentPage == maxPage - 1) return;
				// 오른쪽으로 이동을 위해 현재 페이지를 1 증가
				currentPage++;
			}

			// currentIndex번째 페이지로 Swipe해서 이동
			//StartCoroutine(OnSwipeOneStep(currentPage));
			OnSwipeOneStepAsync(currentPage).Forget();
		}

		async UniTaskVoid OnSwipeOneStepAsync(int index)
		{
			float start = scrollBar.value;
			float current = 0;
			float percent = 0;

			isSwipeMode = true;
			while (percent < 1)
			{
				current += Time.deltaTime;
				percent = current / swipeTime;

				scrollBar.value = Mathf.Lerp(start, scrollPageValues[index], percent);

				await UniTask.Yield(cancellationToken: this.GetCancellationTokenOnDestroy());
			}

			// 아래에 배치된 페이지 버튼 제어
			UpdateCircleContent();
			isSwipeMode = false;
		}



		private void UpdateCircleContent()
		{
			// 아래에 배치된 페이지 버튼 크기, 색상 제어 (현재 머물고 있는 페이지의 버튼만 수정)
			for (int i = 0; i < scrollPageValues.Length; ++i)
			{
				//Debug.Log($"circleContents[i] : {circleContents[i].name}");
				//circleContents[i].localScale = Vector3.one;
				circleContents[i].GetChild(1).GetComponent<Image>().color = new Color(1, 1, 1, 0.3f);
				circleContents[i].GetChild(0).GetComponent<Image>().sprite = nav_bg_off;
				//circleContents[i].GetComponent<Image>().color	= Color.white;

				// 페이지의 절반을 넘어가면 현재 페이지 원을 바꾸도록
				//if ( scrollBar.value < scrollPageValues[i] + (valueDistance / 2) && scrollBar.value > scrollPageValues[i] - (valueDistance / 2) )
				//{
				//	circleContents[i].localScale = Vector3.one * circleContentScale;
				//	//circleContents[i].GetComponent<Image>().color	= Color.black;
				//}
				//if (i != currentPage)
				//	mainComps[i].CompOpen(false);

				//if (i != currentPage)
				//{
				//if (mainComps[i]._open)
				//{
				//Debug.Log($" _open false i : {4-i} ... currentPage : {currentPage}  num : {mainComps[i]._num}");
				mainComps[4 - i].CompOpen(false);
				//}
				//}
			}

			//circleContents[currentPage].localScale = Vector3.one * circleContentScale;
			circleContents[currentPage].transform.GetChild(0).GetComponent<Image>().sprite = nav_bg_on;

			mainComps[4 - currentPage].CompOpen(true);
			practiceMenu.SetVisible(currentPage == 3);
			Vector3 pos = circleContents[currentPage].GetComponent<RectTransform>().anchoredPosition;
			circleContents[currentPage].transform.GetChild(1).GetComponent<Image>().color = new Color(1, 1, 1, 1f);

			string nm = circleContents[currentPage].name;
			//Debug.Log($"page : {4 - currentPage} .. nm : {nm}, pos : {pos}");
			arrUpDn.GetComponent<RectTransform>().anchoredPosition = new Vector3(100f, pos.y, 0);

			if(currentPage == 0)
            {
				arrUpDn.GetChild(1).gameObject.SetActive(false);
			}
            else
            {
				arrUpDn.GetChild(1).gameObject.SetActive(true);
			}
			if (currentPage == 4)
			{
				arrUpDn.GetChild(0).gameObject.SetActive(false);
			}
			else
			{
				arrUpDn.GetChild(0).gameObject.SetActive(true);
			}

		}

	}
}
