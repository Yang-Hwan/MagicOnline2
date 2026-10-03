using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Exert.Network;
using System.Collections.Generic;
using Assets.Scripts.Exert.BackSys;
using System.Linq;

namespace Assets.Scripts.Sight.Surface.Consist
{
    public class RoomSwipe : MonoBehaviour
    {

		[SerializeField] Scrollbar scrollBar;                    // Scrollbar의 위치를 바탕으로 현재 페이지 검사
		[SerializeField] Transform[] circleContents;             // 현재 페이지를 나타내는 원 Image UI들의 Transform
		//[SerializeField] Button btnArrLeft;
		//[SerializeField] Button btnArrRight;

		[SerializeField]
		private float swipeTime = 0.2f;         // 페이지가 Swipe 되는 시간
		[SerializeField]
		private float swipeDistance = 100.0f;        // 페이지가 Swipe되기 위해 움직여야 하는 최소 거리

		//[SerializeField]private TextMeshProUGUI[] txtLst;
		//[SerializeField] private Button[] btnLst;

		public float[] scrollPageValues;           // 각 페이지의 위치 값 [0.0 - 1.0]
		private float valueDistance = 0;            // 각 페이지 사이의 거리
		public int currentPage = -1;            // 현재 페이지
		private int maxPage = 0;                // 최대 페이지
		private float startTouchX;              // 터치 시작 위치
		private float endTouchX;                    // 터치 종료 위치
		private bool isSwipeMode = false;       // 현재 Swipe가 되고 있는지 체크
		private float circleContentScale = 1.3f;    // 현재 페이지의 원 크기(배율)

		private float _resetVal;

		[SerializeField] GameObject pnlMainSwipe;
		[SerializeField] GameObject pnlSubLobby;

		[SerializeField] HallRoom[] hrLst;
		Dictionary<int, HallRoom> hallObj = new Dictionary<int, HallRoom>();

		[SerializeField] TextMeshProUGUI txt01;
		[SerializeField] TextMeshProUGUI txt02;

		private int remainCnt = 0;

		private void Awake()
		{
			// 스크롤 되는 페이지의 각 value 값을 저장하는 배열 메모리 할당
			maxPage = transform.childCount - 0;// scrollPageValues.Length;
			scrollPageValues = new float[maxPage];
			//txtLst = new TextMeshProUGUI[maxPage];
			//btnLst = new Button[maxPage];
			_resetVal = -10000f;

			// 스크롤 되는 페이지 사이의 거리  비율값
			valueDistance = 1f / (transform.childCount - 1f);
			Debug.Log($"scrollPageValues.Length : {scrollPageValues.Length} .. valueDistance : {valueDistance}");

			//hrLst = new HallRoom[maxPage];
			//Debug.Log($"RoomSwipe Awake hrLst len : {hrLst.Length}");
			//Debug.Log($"RoomSwipe Awake transform.GetChild(0).name : {transform.GetChild(0).name}");
			//transform.GetChild(0).GetComponent<HallRoom>().SetNum(0, this);
			//Debug.Log($"RoomSwipe Awake transform.GetChild(1).name : {transform.GetChild(1).name}");

			//btnArrLeft.onClick.AddListener(btnArrLeft_Click);
			//btnArrRight.onClick.AddListener(btnArrRight_Click);
			for (int i = 0; i < transform.childCount; i++)
            {
				if(i == 0 || i > 10)
                {

                }
				//HallRoom hr = transform.GetChild(i).GetComponent<HallRoom>();
				//hr.SetNum(i, this);
				//Debug.Log($"RoomSwipe Awake hr {i} : {hr.name}");

				//hrLst[i] = hr;
				float f = i * 0.093f;
				//Debug.Log($"i : {i}, f : {f}");
				scrollPageValues[i] = f;
			}

	 
			//for (int i = 0; i < scrollPageValues.Length; ++i)
			//{
			//	float f = i * 0.093f;
			//	//Debug.Log($"i : {i}, f : {f}");
			//	scrollPageValues[i] = f;
			//	txtLst[i] = transform.GetChild(i + 1).GetChild(0).GetComponent<TextMeshProUGUI>();
			//	btnLst[i] = transform.GetChild(i + 1).GetComponent<Button>();
			//	btnLst[i].onClick.AddListener(Btn_Click);
			//}
			// 최대 페이지의 수

			//Debug.Log($"maxPage : {maxPage}");
		}

		private void Start()
		{
			string msg = string.Empty;

            for (int a = 0; a < hrLst.Length; a++)
            {
				hrLst[a].SetNum(a, this);
			}

            int c = BackendChart.skillMatchData.Count;
            //string msg00 = "RoomSwipe Start";
            //Debug.Log($"PnlLobby Start LST CNT : {c}");
            //txt01.text = msg00 + " .. c : " + c;
            int i = 1;
            hallObj.Clear();
			int maxCard = 11;
            BackendChart.skillMatchData.ForEach(r =>
            {
                bool isUse = false;
                if (r.PrizeCoin <= NetworkManager.user_coin && NetworkManager.user_coin <= r.MaxCoin)
                {
                    isUse = true;
                }
                int hallIdx = r.HallIdx;
                string title = r.HallName;
                int playerCnt = 0;
                int cushionCnt = r.MatchCushion;
				long prize = r.PrizeCoin;
				int targetHit = r.TargetHit;
				int matchBall = r.MatchBall;
				int finish = r.FinishMission;
				int totMin = r.MatchTotMin;
				if (i <= maxCard)
                {
                    //msg += $"{hallIdx} : {title};  ";
                    hrLst[i].SetRoomInfo(hallIdx, isUse, title, playerCnt, cushionCnt, targetHit, prize, matchBall, finish, totMin);
                    hallObj.Add(hallIdx, hrLst[i]);
                    i++;
                }
            });
            NetworkManager.network.GetRoomsInfo();
			remainCnt = maxCard - i + 1;
			//Debug.Log($"remainCnt : {remainCnt}");
			//msg += $" >> remain : {remainCnt}";
			//txt02.text = msg;
			ResetTouch();
			//CompOpen(false);
			// 최초 시작할 때 0번 페이지를 볼 수 있도록 설정
			//SetScrollBarValue(4);
			WaitStart().Forget();
		}


		async UniTaskVoid WaitStart()
		{
			await UniTask.WaitForSeconds(0.1f, cancellationToken: this.GetCancellationTokenOnDestroy());
			currentPage = -1;
			SetScrollBarValue(0);
		}

		public void GotoWaitRoom(int hallIdx)
        {
			Debug.Log($"Btn_Click hallIdx : {hallIdx}");
			NetworkManager.hallIdx = hallIdx;

			pnlMainSwipe.SetActive(false);
			pnlSubLobby.SetActive(true);
			NetworkManager.network.JoinRandomRoom(hallIdx);


		}



		public void SetScrollBarValue(int index)
		{
			//Debug.Log($"currentPage : {currentPage}, index : {index}, val : {scrollPageValues[index]}");
			if (index == currentPage) {  return; }
			currentPage = index;
			if (currentPage >= maxPage) return;
			scrollBar.value = scrollPageValues[index];
			OnSwipeOneStepAsync(currentPage).Forget();
			//UpdateCircleContent();
		}


        private void OnEnable()
        {
			//Debug.Log("RoomSwipe.OnEnable");
			//NetworkManager.network.OnNetworkLobbyPlayer += NetworkManager_OnNetworkLobbyPlayer;
			NetworkManager.network.OnNetworkRoomUpdate += NetworkManager_OnNetworkRoomUpdate;
		}

		private void OnDisable()
		{
			//Debug.Log("RoomSwipe.OnDisable");
			//NetworkManager.network.OnNetworkLobbyPlayer -= NetworkManager_OnNetworkLobbyPlayer;
			if (NetworkManager.TryGetExistingNetwork(out var existingOnNetworkRoomUpdate))
			    existingOnNetworkRoomUpdate.OnNetworkRoomUpdate -= NetworkManager_OnNetworkRoomUpdate;
		}


		private void Update() => UpdateInput();
		bool isPress = false;

		private void UpdateInput()
		{

			//Debug.Log($"... startTouchX : {startTouchX}, endTouchX : {endTouchX}, cur : {currentPage} .. sb : {scrollBar.value}, curVal : {scrollPageValues[currentPage]}, max : {scrollPageValues[maxPage - 1]}, IS : {scrollBar.value - 0.01f > scrollPageValues[maxPage - 1]}");
			// 현재 Swipe를 진행중이면 터치 불가
			if (isSwipeMode == true)
			{
				//Debug.Log($"if (isSwipeMode == true) CURPAGE : {currentPage}");
				return;
			}

			//if (scrollBar.value - 0.01f > scrollPageValues[maxPage - 1])
			//{
			//	Debug.Log("if (scrollBar.value - 0.01f > scrollPageValues[maxPage - 1])");
			//	scrollBar.value = scrollPageValues[maxPage - 1];
			//	endTouchX = startTouchX;
			//	UpdateSwipe();
			//	return;
			//}

			// 스크롤바위치가 현재구역범위 안에 있다면 
			if (scrollBar.value < scrollPageValues[currentPage] + (valueDistance / 2) &&
				scrollBar.value > scrollPageValues[currentPage] - (valueDistance / 2))
			{
				//Debug.Log($"... cur : {currentPage} .. sb : {scrollBar.value}, curVal : {scrollPageValues[currentPage]} ....... oooooooo");
			}
			else
			{
				// 현재구역 범위 바깥에 있고 클릭이 안된 상태라면
				if (!isPress)
				{
					if (currentPage == 0)
					{
						//Debug.Log("if (currentPage == 0)");
						//scrollBar.value = scrollPageValues[currentPage];
						UpdateSwipe();
						//OnSwipeOneStepAsync(currentPage).Forget();
					}
					else if (currentPage == maxPage - 1)
					{
						//Debug.Log("else if (currentPage == maxPage - 1)");
						//scrollBar.value = scrollPageValues[currentPage];
						UpdateSwipe();
						//OnSwipeOneStepAsync(currentPage).Forget();
					}
				}
				//UpdateSwipe();
				//Debug.Log($"... cur : {currentPage} .. sb : {scrollBar.value}, curVal : {scrollPageValues[currentPage]} ....... xxxxxxxx");
			}




#if UNITY_EDITOR
			// 마우스 왼쪽 버튼을 눌렀을 때 1회
			if (Input.GetMouseButtonDown(0))
			{
				// 터치 시작 지점 (Swipe 방향 구분)
				startTouchX = Input.mousePosition.x;
				isPress = true;

				//Debug.Log($"startTouchX : {startTouchX}");
			}
			else if (Input.GetMouseButton(0))
			{
				endTouchX = Input.mousePosition.x;
			}
			else if (Input.GetMouseButtonUp(0))
			{
				// 터치 종료 지점 (Swipe 방향 구분)
				endTouchX = Input.mousePosition.x;
				isPress = false;

				UpdateSwipe();
			}

			else
			{
				isPress = false;
			}

#endif

			if (Input.touchCount == 1)
			{
				Touch touch = Input.GetTouch(0);

				if (touch.phase == TouchPhase.Began)
				{
					// 터치 시작 지점 (Swipe 방향 구분)
					startTouchX = touch.position.x;
				}
				else if (touch.phase == TouchPhase.Moved)
				{
					endTouchX = touch.position.x;
					isPress = true;
				}
				else if (touch.phase == TouchPhase.Ended)
				{
					// 터치 종료 지점 (Swipe 방향 구분)
					endTouchX = touch.position.x;

					isPress = false;

					UpdateSwipe();
				}
			}
#if UNITY_ANDROID
#endif
		}




		private void UpdateSwipe()
		{

			// 드래그가 없는 거리를  Swipe X
			if (startTouchX - endTouchX == 0)
			{
				//Debug.Log($"if(startTouchX : {startTouchX} - endTouchX : {endTouchX} == 0)");
				return;
			}

			// 너무 작은 거리를 움직였을 때는 Swipe X
			if (Mathf.Abs(startTouchX - endTouchX) < swipeDistance)
			{
				//Debug.Log($"startTouchX : {startTouchX}, endTouchX : {endTouchX}, swipeDistance : {swipeDistance}");
				// 원래 페이지로 Swipe해서 돌아간다
				OnSwipeOneStepAsync(currentPage).Forget();
				return;
			}

			// Swipe 방향
			bool isLeft = startTouchX < endTouchX ? true : false;
			//Debug.Log($"1111. startTouchX : {startTouchX} - endTouchX : {endTouchX} ... isLeft : {isLeft}, currentPage : {currentPage} ");

			// 이동 방향이 왼쪽일 때
			if (isLeft)
			{
				// 현재 페이지가 왼쪽 끝이면 종료
				if (currentPage != 0)
				{
					// 왼쪽으로 이동을 위해 현재 페이지를 1 감소
					currentPage--;
				}
			}
			// 이동 방향이 오른쪽일 떄
			else
			{
				// 현재 페이지가 오른쪽 끝이면 종료
				if (currentPage <= maxPage - 5 - remainCnt)
				{
					// 오른쪽으로 이동을 위해 현재 페이지를 1 증가
					currentPage++;
					//Debug.Log(currentPage);

				}
			}
			


			//Debug.Log($"2222. startTouchX : {startTouchX} - endTouchX : {endTouchX} ... isLeft : {isLeft}, currentPage : {currentPage} ");
			// currentIndex번째 페이지로 Swipe해서 이동
			OnSwipeOneStepAsync(currentPage).Forget();
		}


		async UniTaskVoid OnSwipeOneStepAsync(int index)
		{
			float start = scrollBar.value;
			float end = scrollPageValues[index];
			float current = 0;
			float percent = 0;
			//if(start > scrollPageValues[index])
			//         {
			//	//Debug.Log("if(start > scrollPageValues[index])");
			//	start = scrollPageValues[index];
			//}

			//Debug.Log($"OnSwipeOneStepAsync index : {index},  start : {start}, end : {end} ");

			isSwipeMode = true;
			while (percent < 1)
			{
				current += Time.deltaTime;
				percent = current / swipeTime;

				scrollBar.value = Mathf.Lerp(start, end, percent);

				await UniTask.Yield(cancellationToken: this.GetCancellationTokenOnDestroy());
			}

			ResetTouch();
			// 아래에 배치된 페이지 버튼 제어
			UpdateCircleContent();
			isSwipeMode = false;
		}

		void ResetTouch()
		{
			//Debug.Log();
			startTouchX = _resetVal;
			endTouchX = _resetVal;
		}

		private void UpdateCircleContent()
		{
			// 아래에 배치된 페이지 버튼 크기, 색상 제어 (현재 머물고 있는 페이지의 버튼만 수정)
			for (int i = 0; i < scrollPageValues.Length; ++i)
			{
				if(i != currentPage+1)
					hrLst[i].SetCardOn(false);

				//txtLst[i].color = Color.white;

				//circleContents[i].localScale = Vector3.one;
				//circleContents[i].GetComponent<Image>().color	= Color.white;

				// 페이지의 절반을 넘어가면 현재 페이지 원을 바꾸도록
				if (scrollBar.value < scrollPageValues[i] + (valueDistance / 2) && scrollBar.value > scrollPageValues[i] - (valueDistance / 2))
				{
					//txtLst[i].color = Color.red;

					//circleContents[i].localScale = Vector3.one * circleContentScale;
					//circleContents[i].GetComponent<Image>().color	= Color.black;
				}
			}
			
			//Debug.Log($"hrLst.Length : {hrLst.Length} .. currentPage+1 : {currentPage + 1} .. {hrLst[1].name}");


			hrLst[currentPage+1].SetCardOn(true);
			//txtLst[currentPage].color = Color.blue;
		}

		void NetworkManager_OnNetworkLobbyPlayer(int playerCnt)
		{
			//DelayLobbyPlayer(playerCnt).Forget();
			// Debug.Log("NetworkManager_OnNetworkLobbyPlayer cnt : " + playerCnt);
			//txtLobby.text = playerCnt.ToString();
		}


		async UniTaskVoid DelayLobbyPlayer(int playerCnt)
		{
			await UniTask.WaitForSeconds(1.5f);
			//txtLobby.text = playerCnt.ToString();

			//Debug.Log($"DelayRoom ....... ");
			//dic.Keys.ToList().ForEach(k =>
			//{
			//    Debug.Log($"NetworkManager_OnNetworkRoomUpdate key : {k}, val : {dic[k]}");
			//    hallObj[k].SetHallPlayerCnt(dic[k]);
			//});
		}

		void NetworkManager_OnNetworkRoomUpdate(Dictionary<int, int> dic)
        {
			dic.Keys.ToList().ForEach(k =>
			{
				//Debug.Log($"NetworkManager_OnNetworkRoomUpdate key : {k}, val : {dic[k]}");
				hallObj[k].SetHallPlayerCnt(dic[k]);
			});

			//Debug.Log($"DelayRoom ....... ");
			//DelayRoom(dic).Forget();
		}

		async UniTaskVoid DelayRoom(Dictionary<int, int> dic)
		{
			await UniTask.WaitForSeconds(0.5f, cancellationToken: this.GetCancellationTokenOnDestroy());
			//Debug.Log($"DelayRoom ....... ");
			dic.Keys.ToList().ForEach(k =>
			{
				//Debug.Log($"NetworkManager_OnNetworkRoomUpdate key : {k}, val : {dic[k]}");
				hallObj[k].SetHallPlayerCnt(dic[k]);
			});
		}

		public void btnArrLeft_Click()
        {
			if (currentPage > 0)
			{
				currentPage--;
				OnSwipeOneStepAsync(currentPage).Forget();
			}
			//Debug.Log("btnArrLeft_Click");

		}

		public void btnArrRight_Click()
		{
			if (currentPage <= maxPage - 5 - remainCnt)
			{
				// 오른쪽으로 이동을 위해 현재 페이지를 1 증가
				currentPage++;
				OnSwipeOneStepAsync(currentPage).Forget();
			}
			//Debug.Log($"btnArrRight_Click maxPage : {maxPage}, currentPage : {currentPage}");

		}

	}
}
