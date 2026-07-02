# Boids_Job

Unity ECS의 Job System과 Burst Compiler를 활용해 대규모 Boids(군집 행동) 시뮬레이션을 최적화하는 과정을 다루는 프로젝트다. 물고기 군집을 구현 대상으로 삼아, 메인스레드 단일 루프 구조에서 시작해 Job 병렬화, 장애물 회피, GPU 기반 애니메이션까지 단계적으로 확장한다.

각 단계는 별도의 씬과 독립된 스크립트 세트로 구성되어, 씬 간 의존성 없이 단계별 비교가 가능하도록 한다.

## 프로젝트 구성

| 씬 | 설명 |
| --- | --- |
| `00_Fish Swim Scene` | 물고기 유영 셰이더 (`FishSwim.shader` 등) |
| `01_Boids Scene` | `BoidsManager` 기반 O(N²) 메인스레드 구현 |
| `02_Job Scene` | `BoidsManagerJob` — Job System + Burst Compiler 적용, Force 계산과 위치 업데이트를 분리 |
| `03_Improved Scene` | `BoidsManagerImproved` — Cohesion 반경 분리, maxNeighbors 캡, Wander, Leveling, AABB Soft Zone 경계 처리 추가 |
| `04_Avoid Scene` | `BoidsManagerObstacle` — SpherecastCommand 기반 장애물 회피 추가 |

### Job 파이프라인 (`04_Avoid Scene` 기준)

```
PrepareRaycastJob
  → SpherecastCommand.ScheduleBatch
  → ObstacleAvoidJob
  → BoidForceObstacleJob
  → UpdatePositionObstacleJob
```

JobHandle 의존성 체인으로 연결되며, Force 계산(`IJobParallelFor`)과 위치·회전 업데이트(`IJobParallelForTransform`)를 분리해 동일 프레임 내 Race Condition을 방지한다.

### 물고기 유영 애니메이션

CPU 기반 런타임 메시 변형에서 GPU Vertex Shader 기반 변형(`FishSwim.shader`)으로 전환해, 다수의 개체를 처리할 때의 성능 문제를 해결했다.

## 버전 정보

| 항목 | 버전 |
| --- | --- |
| Unity Editor | 6000.3.10f1 (6.3 LTS) |
| Render Pipeline | URP |

## 관련 글

구현 과정과 설계 의도는 기술 블로그에 시리즈로 정리되어 있다.

1. [Unity로 구현하는 Boids 시뮬레이션](https://hamin321.tistory.com/entry/Unity%EB%A1%9C-%EA%B5%AC%ED%98%84%ED%95%98%EB%8A%94-Boids-%EC%8B%9C%EB%AE%AC%EB%A0%88%EC%9D%B4%EC%85%98-%E2%80%94-%EB%AC%BC%EA%B3%A0%EA%B8%B0-%EA%B5%B0%EC%A7%91-AI-%EB%A7%8C%EB%93%A4%EA%B8%B0) — Separation / Alignment / Cohesion, O(N²) 메인스레드 구현
2. [Job System을 활용한 Boids 시뮬레이션 최적화](https://hamin321.tistory.com/entry/UnityC-Job-System%EC%9D%84-%ED%99%9C%EC%9A%A9%ED%95%9C-Boids-%EC%8B%9C%EB%AE%AC%EB%A0%88%EC%9D%B4%EC%85%98-%EC%B5%9C%EC%A0%81%ED%99%94) — Job System + Burst Compiler 적용, Force 계산과 위치 업데이트 분리
3. [물고기 유영 시뮬레이션 구현](https://hamin321.tistory.com/entry/%EB%AC%BC%EA%B3%A0%EA%B8%B0-%EC%9C%A0%EC%98%81-%EC%8B%9C%EB%AE%AC%EB%A0%88%EC%9D%B4%EC%85%98-%EA%B5%AC%ED%98%84) — Vertex Shader 기반 물고기 유영 애니메이션
4. [Boids 시뮬레이션 — 군집 행동 고도화](https://hamin321.tistory.com/entry/UnityC-Boids-%EC%8B%9C%EB%AE%AC%EB%A0%88%EC%9D%B4%EC%85%98-%E2%80%94-%EA%B5%B0%EC%A7%91-%ED%96%89%EB%8F%99-%EA%B3%A0%EB%8F%84%ED%99%94) — Cohesion 반경 분리, maxNeighbors, AABB Soft Zone 경계, Wander, Leveling

## 3D 모델 크레딧

이 프로젝트는 다음의 Creative Commons Attribution 4.0 라이선스 3D 모델을 사용한다.

- "Fish" (https://skfb.ly/6U7u6) by Yimit is licensed under [Creative Commons Attribution](http://creativecommons.org/licenses/by/4.0/).
- "Lowpoly Rocks - 1" (https://skfb.ly/6yMPo) by Loïc Norgeot is licensed under [Creative Commons Attribution](http://creativecommons.org/licenses/by/4.0/).

## License

이 저장소의 코드는 MIT License를 따른다. 단, 위 3D 모델은 각 라이선스 조건(저작자 표시)을 따른다
