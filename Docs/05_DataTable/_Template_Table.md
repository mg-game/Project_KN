# TBL_<테이블명>

| 항목 | 값 |
|---|---|
| 상태 | Draft |
| 원본 | (Google Sheets URL 또는 파일 경로) |
| 변환 출력 | `Assets/_Project/Data/Tables/<테이블명>.json` |
| 사용 시스템 | SYS_ |

## 1. 컬럼 정의
| Column | Type | Key | Nullable | 설명 | 예시 |
|---|---|---|---|---|---|
| Id | int | PK | N | 고유 ID | 100001 |
| NameKey | string | FK → TBL_Localize.Key | N | 이름 로컬라이즈 키 | CHAR_NAME_100001 |

## 2. 관계
```
TBL_<이 테이블>.Id  1 ──── N  TBL_<참조 테이블>.<컬럼>
```

## 3. ID 대역 규칙
| 대역 | 용도 |
|---|---|
| 100000~199999 | |

## 4. 검증 규칙 (변환 툴에서 체크)
- [ ] PK 중복 없음
- [ ] FK 대상 존재
- [ ] 수치 범위:
