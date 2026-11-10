using NUnit.Framework;

namespace IdleGame.Tests
{
    /// <summary>메타 팝업 큐 (F-05): 종류 순서, 중복 합침(복귀는 최신 값), 접속 모달 상한 3, 세션 모달은 스테이지 사이에만</summary>
    public class PopupQueueTests
    {
        PopupQueue _queue;
        int _enqueued;

        [SetUp]
        public void SetUp()
        {
            _queue = new PopupQueue();
            _enqueued = 0;
            _queue.Enqueued += () => _enqueued++;
        }

        MetaPopupKind Next(bool betweenStages = true)
        {
            Assert.IsTrue(_queue.TryDequeue(betweenStages, false, out var request), "꺼낼 것이 있어야 함");
            return request.Kind;
        }

        [Test]
        public void Dequeue_FollowsKindOrder_NotInsertOrder()
        {
            _queue.Enqueue(MetaPopupRequest.FeatureUnlock(FeatureId.Gacha));
            _queue.Enqueue(MetaPopupRequest.Attendance());
            _queue.Enqueue(MetaPopupRequest.FtueGraduation());
            _queue.Enqueue(MetaPopupRequest.Return(3600));
            _queue.Enqueue(MetaPopupRequest.Notice("n1"));

            Assert.AreEqual(MetaPopupKind.Return, Next());
            Assert.AreEqual(MetaPopupKind.Attendance, Next());
            Assert.AreEqual(MetaPopupKind.Notice, Next());
            Assert.AreEqual(MetaPopupKind.FeatureUnlock, Next());
            Assert.AreEqual(MetaPopupKind.FtueGraduation, Next());
            Assert.IsTrue(_queue.IsEmpty);
            Assert.AreEqual(5, _enqueued);
        }

        [Test]
        public void SameKindKeepsInsertOrder_AndDuplicatesMerge()
        {
            _queue.Enqueue(MetaPopupRequest.FeatureUnlock(FeatureId.Gacha));
            _queue.Enqueue(MetaPopupRequest.FeatureUnlock(FeatureId.Expedition));
            _queue.Enqueue(MetaPopupRequest.FeatureUnlock(FeatureId.Gacha));
            Assert.AreEqual(2, _queue.Count, "같은 기능 카드는 하나만");
            Assert.AreEqual(2, _enqueued);

            Assert.IsTrue(_queue.TryDequeue(true, false, out var first));
            Assert.AreEqual(FeatureId.Gacha, first.Feature);
            Assert.IsTrue(_queue.TryDequeue(true, false, out var second));
            Assert.AreEqual(FeatureId.Expedition, second.Feature);
        }

        [Test]
        public void Return_IsReplacedByLatestAwayTime()
        {
            _queue.Enqueue(MetaPopupRequest.Return(700));
            _queue.Enqueue(MetaPopupRequest.Return(5000));

            Assert.AreEqual(1, _queue.Count);
            Assert.IsTrue(_queue.TryPeek(out var request));
            Assert.AreEqual(5000, request.Value);
        }

        [Test]
        public void StartupBatch_CapsModalsAtThree_DropsTheRestButEventStart()
        {
            _queue.BeginStartupBatch();
            _queue.Enqueue(MetaPopupRequest.Return(3600));
            _queue.Enqueue(MetaPopupRequest.Attendance());
            _queue.Enqueue(MetaPopupRequest.Notice("a"));
            _queue.Enqueue(MetaPopupRequest.Notice("b"));
            _queue.Enqueue(new MetaPopupRequest(MetaPopupKind.EventStart, key: "evt"));
            _queue.Enqueue(MetaPopupRequest.FeatureUnlock(FeatureId.Shop));

            Assert.AreEqual(MetaPopupKind.Return, Next(betweenStages: false), "접속 묶음은 전투 중이어도 바로");
            Assert.AreEqual(MetaPopupKind.Attendance, Next(false));
            Assert.AreEqual(MetaPopupKind.Notice, Next(false));
            Assert.AreEqual(3, _queue.StartupShown);
            Assert.IsFalse(_queue.TryDequeue(true, false, out _), "상한을 넘음");
            Assert.IsFalse(_queue.Contains(MetaPopupKind.Notice), "넘친 접속 요청은 버린다");
            Assert.IsTrue(_queue.Contains(MetaPopupKind.EventStart), "이벤트 시작 카드는 상한에 걸려도 남는다 (§2.9)");
            Assert.IsTrue(_queue.Contains(MetaPopupKind.FeatureUnlock), "세션 모달은 남는다");
            Assert.IsFalse(_queue.TryReserveStartupModal(), "공지 묶음 자리도 없음");
            Assert.IsFalse(_queue.TryDequeue(true, false, out _), "상한을 넘으면 묶음 중에는 남은 이벤트 카드도 꺼내지 않는다");

            _queue.EndStartupBatch();
            Assert.IsFalse(_queue.TryDequeue(false, false, out _), "세션 모달은 스테이지 사이에만");
            Assert.AreEqual(MetaPopupKind.EventStart, Next(true), "남긴 이벤트 카드가 먼저 (종류 순서)");
            Assert.AreEqual(MetaPopupKind.FeatureUnlock, Next(true));
        }

        [Test]
        public void StartupBatch_SessionModalsWaitForBatchEnd()
        {
            _queue.Enqueue(MetaPopupRequest.FeatureUnlock(FeatureId.Gacha));
            _queue.BeginStartupBatch();
            _queue.Enqueue(MetaPopupRequest.Attendance());

            Assert.AreEqual(MetaPopupKind.Attendance, Next(true));
            Assert.IsFalse(_queue.TryDequeue(true, false, out _), "접속 묶음 중에는 세션 모달을 꺼내지 않는다");

            _queue.EndStartupBatch();
            Assert.AreEqual(MetaPopupKind.FeatureUnlock, Next(true));
        }

        [Test]
        public void ReserveStartupModal_CountsTowardCap()
        {
            Assert.IsTrue(_queue.TryReserveStartupModal(), "묶음 밖에서는 항상 true");
            Assert.AreEqual(0, _queue.StartupShown);

            _queue.BeginStartupBatch();
            _queue.Enqueue(MetaPopupRequest.Return(3600));
            _queue.Enqueue(MetaPopupRequest.Attendance());
            Next(false);
            Assert.IsTrue(_queue.TryReserveStartupModal(), "공지 묶음 = 1자리");
            Next(false);
            Assert.AreEqual(3, _queue.StartupShown);
            Assert.IsFalse(_queue.TryReserveStartupModal());
        }

        [Test]
        public void OpenPopup_BlocksEverything()
        {
            _queue.BeginStartupBatch();
            _queue.Enqueue(MetaPopupRequest.Return(3600));
            Assert.IsFalse(_queue.TryDequeue(true, true, out _));
            Assert.AreEqual(1, _queue.Count);
            Assert.AreEqual(0, _queue.StartupShown);
        }

        [Test]
        public void OutsideBatch_StartupKindsAlsoWaitForStageBreak()
        {
            _queue.Enqueue(MetaPopupRequest.Attendance());
            Assert.IsFalse(_queue.TryDequeue(false, false, out _), "세션 중 날이 바뀌어 들어온 출석부도 스테이지 사이에");
            Assert.AreEqual(MetaPopupKind.Attendance, Next(true));
        }

        [Test]
        public void RemoveAndClear()
        {
            _queue.Enqueue(MetaPopupRequest.FeatureUnlock(FeatureId.Gacha));
            _queue.Enqueue(MetaPopupRequest.FeatureUnlock(FeatureId.Shop));
            _queue.Enqueue(MetaPopupRequest.Attendance());

            Assert.IsTrue(_queue.Remove(MetaPopupKind.FeatureUnlock));
            Assert.AreEqual(1, _queue.Count);
            Assert.IsFalse(_queue.Remove(MetaPopupKind.ChapterChest));

            _queue.BeginStartupBatch();
            _queue.Clear();
            Assert.IsTrue(_queue.IsEmpty);
            Assert.IsFalse(_queue.InStartupBatch);
        }

        [Test]
        public void KindHelpers()
        {
            Assert.IsTrue(PopupQueue.IsStartupKind(MetaPopupKind.Return));
            Assert.IsTrue(PopupQueue.IsStartupKind(MetaPopupKind.EventStart));
            Assert.IsFalse(PopupQueue.IsStartupKind(MetaPopupKind.FeatureUnlock));
            Assert.IsTrue(PopupQueue.RequiresBetweenStages(MetaPopupKind.FtueGraduation));
            Assert.IsFalse(PopupQueue.RequiresBetweenStages(MetaPopupKind.Attendance));
        }
    }
}
