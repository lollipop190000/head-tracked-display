# Head Tracked Display

일반 모니터를 3D 공간의 창처럼 보이게 하는 Unity 패키지입니다. 웹캠에서 두 눈의 중심을 추적하고 화면의 실제 크기와 관찰자 위치로 **off-axis projection**을 계산합니다. 장면 전체에 적용되므로 3D 모델마다 추적 스크립트를 붙일 필요가 없습니다. 한 사람을 위한 단안 화면 효과이며 입체 디스플레이는 아닙니다.

![Unity URP 시연 장면의 기본 시점](docs/demo-neutral.png)

## 구성

- `Packages/com.headtracked.display`: 재사용 가능한 Unity Package Manager 패키지. `HeadTrackedDisplay`, 보정 데이터, 투영 계산, Python TCP 추적 소스가 들어 있습니다. MediaPipe Unity Plugin을 설치하면 Unity 내부 추적 소스도 활성화됩니다.
- `Demo`: Unity 6.3 LTS + URP 시연 프로젝트. Kenney의 FBX 메시 3개와 앞뒤 깊이를 확인할 수 있는 장면을 생성합니다.
- `python_tracker`: 공식 MediaPipe Python 추적 프로그램 및 카메라 정밀 보정 도구.

## 이 PC에서 바로 테스트 (Windows)

`Run-HeadTrackedDemo.cmd`를 더블클릭하면 로컬 Windows 빌드와 Python 웹캠 추적기가 함께 시작됩니다. 기본 웹캠은 0번입니다. 1번 웹캠을 쓰려면 PowerShell에서 `./Run-HeadTrackedDemo.cmd 1`을 실행합니다. 시연 앱은 전체 화면으로 열리며 **Alt+F4**로 닫으면 추적기도 종료됩니다. 화면 왼쪽의 **Tracking: FACE FOUND**를 확인하고 좌우·상하·앞뒤로 머리를 움직여 보세요. **NO FACE**라면 웹캠 번호와 조명을 확인합니다. 그다음 실제 화면 크기와 웹캠 위치를 입력하고 기준 거리에서 **Capture reference distance**를 누릅니다.

로컬 빌드는 `Builds/HeadTrackedDemo/HeadTrackedDemo.exe`에 있으며 Git에는 포함하지 않습니다. 새로 내려받은 저장소에서는 아래 설치 절차를 따른 뒤 Unity 6.3 Editor에서 Windows x64 빌드를 만들어야 합니다.

## 빠른 시작: Python 추적

1. Unity Hub에서 **Unity 6.3 LTS** Editor를 설치하고 `Demo` 폴더를 프로젝트로 엽니다. 이 저장소의 `Demo/Packages/manifest.json`은 옆에 있는 로컬 패키지를 참조합니다.
2. Windows PowerShell에서 저장소 루트로 이동해 실행합니다.

   ```powershell
   py -3 -m venv python_tracker/.venv
   python_tracker/.venv/Scripts/python.exe -m pip install -r python_tracker/requirements.txt
   python python_tracker/download_model.py
   ```

3. Unity 메뉴 **Head Tracked → Create or refresh demo scene**을 한 번 실행해 URP 설정을 생성합니다. `Assets/Scenes/HeadTrackedDemo.unity`에서 Play를 누르면 환경과 FBX 모델이 생성됩니다.
4. 별도 PowerShell에서 추적기를 실행합니다. 카메라 목록은 `--list-cameras`로 확인하고 다른 웹캠은 `--camera 1`처럼 지정합니다.

   ```powershell
   python_tracker/.venv/Scripts/python.exe python_tracker/tracker.py --camera 0
   ```

5. 화면에서 얼굴이 잡히면 모니터의 실제 가로·세로 길이와 웹캠 렌즈 위치를 입력합니다. 화면에서 눈까지의 거리를 자로 재어 **Reference eye distance**에 넣고, 그 위치에서 **Capture reference distance**를 누릅니다. **Save calibration**으로 설정을 저장합니다.

Python 프로그램은 얼굴의 두 눈 위치만 `127.0.0.1:8765`로 전송합니다. 영상 프레임은 Unity로 전송하지 않습니다. 외부 접속을 받지 않습니다.

## Unity 내부 MediaPipe 추적

저장소 루트에서 다음 명령을 실행한 후 Unity가 패키지를 다시 가져올 때까지 기다립니다. 약 290 MB의 MediaPipe Unity Plugin v0.16.3 릴리스를 내려받으며, 바이너리는 Git에 넣지 않습니다.

```powershell
python tools/setup_native_plugin.py
python python_tracker/download_model.py
```

시연 장면의 **Unity MediaPipe** 버튼을 누르면 Unity 프로세스에서 직접 얼굴을 추적합니다. 웹캠 목록에서 장치를 바꿀 수 있습니다. 이 플러그인의 Windows CPU 모드는 제작자가 실험적이라고 표시하므로 실제 PC에서 확인해야 합니다. 플러그인을 설치하지 않은 프로젝트에서도 패키지의 나머지 부분은 사용할 수 있습니다.

## 보정 방법

기본 설정은 화면 크기 53×30 cm, 웹캠은 화면 중심보다 17 cm 위, 화면에서 2.5 cm 앞, 기준 눈 거리 60 cm입니다. 모두 실제 장비에 맞게 수정해야 합니다. 좌우 움직임이 반대로 보이면 **Mirror webcam X**를 전환합니다. 웹캠이 아래로 기울어져 있으면 **Webcam pitch**를 조절합니다. 정확한 창 효과를 위해 시연 앱은 전체 화면에서 사용하고, 실제 화면의 가로세로 비율과 렌더링 비율을 맞춥니다.

정밀 카메라 보정은 `tools/checkerboard_9x6_24mm.svg`를 **A4 가로 방향, 실제 크기 100%**로 인쇄한 뒤, 한 칸이 24 mm인지 자로 확인하고 진행합니다. 웹캠 앞에서 체커보드를 다양한 위치·각도로 보여 주며 Space로 최소 12장을 수집합니다.

```powershell
python_tracker/.venv/Scripts/python.exe python_tracker/calibrate_camera.py --camera 0
```

완료 후 Unity 화면에서 **Reload camera intrinsics**와 **Use precise camera calibration**을 선택하고 실제 두 눈 사이 거리(IPD)를 mm 단위로 입력합니다. 일반 웹캠의 단일 영상만으로 절대 거리를 완벽하게 알 수는 없으며, 얼굴 회전이나 안경·조명에 따라 추정 오차가 납니다.

## 다른 Unity 프로젝트에서 사용

Unity Package Manager에서 `Packages/com.headtracked.display/package.json`을 로컬 패키지로 추가합니다. 렌더 카메라에 `HeadTrackedDisplay`와 추적 소스 하나를 붙이고, `screenPlane`을 실제 화면 중심에 해당하는 Transform으로 지정합니다. 그 Transform의 **로컬 +Z가 화면 안쪽 3D 공간**, -Z가 관찰자 쪽입니다. 스케일은 `(1,1,1)`로 둡니다. `DisplayCalibration`의 화면 크기·웹캠 위치를 설정하면 모든 일반 3D 메시가 같은 시점 투영을 사용합니다. 현재 눈 위치와 추적 상태는 `EyePositionMeters`, `IsTracking`, `Confidence`, `PoseUpdated`로 읽을 수 있어 나중에 게임 상호작용에 사용할 수 있습니다.

현재 `Confidence`는 Face Landmarker가 얼굴을 반환했는지 나타내는 0 또는 1 값입니다. MediaPipe Tasks가 이 결과에 대한 연속적인 확률 점수를 제공하지 않으므로, 추적 품질의 세밀한 지표로 해석하면 안 됩니다.

## 확인과 제한

- Python 프로토콜 검사: `python_tracker/.venv/Scripts/python.exe -m unittest discover -s python_tracker -p 'test_*.py' -v`
- Unity 계산 검사: Test Runner의 EditMode에서 `HeadTracked.Display.Tests` 실행. 화면 네 모서리 투영, 좌우·상하 시차, 앞뒤 거리 변화, 기준 거리 보정을 검사합니다.
- Unity 장면 검사: PlayMode의 `HeadTracked.Demo.SceneTests`는 실제 FBX 메시가 화면 앞뒤에 생성되고 모델별 스크립트 없이 동작하는지 확인합니다. 네이티브 플러그인과 모델 파일을 설치하면 `NativeWebcamTests`가 실제 웹캠 프레임 처리도 확인합니다.
- 현재 자동 검증 결과: Unity EditMode 7/7, 시연 PlayMode 1/1, 네이티브 플러그인 실제 웹캠 프레임 처리 1/1, Python 프로토콜 2/2가 통과했습니다. 웹캠 0번과 1번에서 Python Face Landmarker가 각각 40프레임을 처리했습니다.
- 사람이 카메라 앞에서 좌우·상하·앞뒤로 움직이는 검사와 얼굴 소실·재진입, 좌우 반전 확인은 아직 수행하지 못했습니다. 사용 시 두 추적 방식으로 각각 확인해야 합니다.

화면 앞에 있는 물체는 모니터 테두리에서 잘립니다. 조명과 재질은 시연 장면의 예시이며, 실제 방의 조명과 모니터 색을 맞추면 물체의 존재감이 더 좋아집니다. 현재 구현은 한 사람의 두 눈 중심 위치를 사용하며 개별 눈 영상이나 다중 관찰자 시점은 제공하지 않습니다.

코드는 MIT 라이선스입니다. 예제 메시와 의존성 출처는 [THIRD_PARTY.md](THIRD_PARTY.md)에 기록했습니다.
