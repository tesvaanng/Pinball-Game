using System;
using System.Collections.Generic;
using UnityEngine;
using Pinball.Core;

namespace Pinball.Board
{
    public class BoardManager : Singleton<BoardManager>, ISetupInitializer
    {
        public BallController ballPrefab;
        public Transform shootPoint;
        public float shootForce = 10f;
        public float ballLifeTime = 8f;

        public float spawnOffSet = 0.5f;

        public event Action<int, BigNumber> OnBallScoreChanged;
        public event Action<int, BigNumber> OnPocketEntered;
        public event Action<int> OnBallTimedOut;

        private List<BallController> balls = new List<BallController>();
        private int nextBallId = 1;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            SetupRegistry.RegisterInitializer(this);
        }

        public int ActiveBallCount
        {
            get { return balls.Count; }
        }

        public void Setup(ISetupReporter reporter)
        {
            if (ballPrefab == null)
            {
                Debug.LogWarning("BoardManager: ballPrefab is missing.");
            }

            if (shootPoint == null)
            {
                Debug.LogWarning("BoardManager: shootPoint is missing.");
            }

            reporter.ReportReady(this);
        }

        public BallController ShootBall(float power01)
        {
            if (ballPrefab == null || shootPoint == null)
            {
                return null;
            }
            //for test
            Vector3 spawnOffSetVector = new Vector3(0f, 0f, UnityEngine.Random.Range(-spawnOffSet, spawnOffSet));

            BallController ball = Instantiate(ballPrefab, shootPoint.position + spawnOffSetVector, shootPoint.rotation);
            ball.ballId = nextBallId;
            ball.maxLifeTime = ballLifeTime;
            nextBallId++;
            balls.Add(ball);

            Rigidbody body = ball.GetComponent<Rigidbody>();
            if (body != null)
            {
                Vector3 direction = new Vector3(-shootPoint.up.x, -shootPoint.up.y, -shootPoint.up.z);
                body.AddForce(direction * (shootForce * power01), ForceMode.Impulse);
            }

            return ball;
        }

        public void ReportScoreChanged(BallController ball)
        {
            if (ball == null || OnBallScoreChanged == null)
            {
                return;
            }

            OnBallScoreChanged(ball.ballId, ball.score);
        }

        public void ReportPocket(BallController ball, BigNumber multiplier)
        {
            if (ball == null || OnPocketEntered == null)
            {
                return;
            }

            OnPocketEntered(ball.ballId, multiplier);
        }

        public void ReportTimeout(BallController ball)
        {
            if (ball == null || OnBallTimedOut == null)
            {
                return;
            }

            OnBallTimedOut(ball.ballId);
        }

        public BigNumber GetBallScore(int ballId)
        {
            BallController ball = GetBall(ballId);
            if (ball == null)
            {
                return BigNumber.Zero;
            }

            return ball.score;
        }

        public BigNumber GetRepresentativeBallScore()
        {
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                if (balls[i] != null)
                {
                    return balls[i].score;
                }
            }

            return BigNumber.Zero;
        }

        public void RemoveBall(int ballId)
        {
            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i] == null)
                {
                    balls.RemoveAt(i);
                    i--;
                    continue;
                }

                if (balls[i].ballId != ballId)
                {
                    continue;
                }

                balls[i].reported = true;
                Destroy(balls[i].gameObject);
                balls.RemoveAt(i);
                return;
            }
        }

        public void ClearBalls()
        {
            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i] == null)
                {
                    continue;
                }

                balls[i].reported = true;
                Destroy(balls[i].gameObject);
            }

            balls.Clear();
        }

        private BallController GetBall(int ballId)
        {
            for (int i = 0; i < balls.Count; i++)
            {
                if (balls[i] != null && balls[i].ballId == ballId)
                {
                    return balls[i];
                }
            }

            return null;
        }
    }
}
