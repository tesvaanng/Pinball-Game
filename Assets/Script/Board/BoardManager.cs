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

        [Tooltip("沿垂直於發射方向的偏移量，避免多顆球完全重疊在同一點。")]
        public bool isShootOffsetEnabled = true;
        public float spawnOffSet = 0.5f;

        public event Action<int, BigNumber, BigNumber, Vector2> OnBallScoreChanged;
        public event Action<int, BigNumber, Vector2> OnPocketEntered;
        public event Action<int, Vector2> OnBallTimedOut;

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

        public Vector2 GetLaunchDirection()
        {
            if (shootPoint == null)
            {
                return Vector2.down;
            }

            Vector3 shootVect = -shootPoint.up;
            Vector2 direction = new Vector2(shootVect.x, shootVect.y);
            if (direction.sqrMagnitude < 0.0001f)
            {
                return Vector2.down;
            }

            return direction.normalized;
        }

        public BallController ShootBall(float power01)
        {
            if (ballPrefab == null || shootPoint == null)
            {
                return null;
            }

            Vector2 direction = GetLaunchDirection();
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            Vector2 offset = Vector2.zero;
            if (isShootOffsetEnabled)
            {
                offset = perpendicular * UnityEngine.Random.Range(-spawnOffSet, spawnOffSet);
            }

            BallController ball = Instantiate(ballPrefab, shootPoint.position + (Vector3)offset, shootPoint.rotation);
            ball.ballId = nextBallId;
            ball.maxLifeTime = ballLifeTime;
            nextBallId++;
            balls.Add(ball);

            Rigidbody2D body = ball.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.AddForce(direction * (shootForce * power01), ForceMode2D.Impulse);
            }

            return ball;
        }

        public void ReportScoreChanged(BallController ball, BigNumber delta)
        {
            if (ball == null || OnBallScoreChanged == null)
            {
                return;
            }

            OnBallScoreChanged(ball.ballId, ball.score, delta, ball.transform.position);
        }

        public void ReportPocket(BallController ball, BigNumber multiplier)
        {
            if (ball == null || OnPocketEntered == null)
            {
                return;
            }

            OnPocketEntered(ball.ballId, multiplier, ball.transform.position);
        }

        public void ReportTimeout(BallController ball)
        {
            if (ball == null || OnBallTimedOut == null)
            {
                return;
            }

            OnBallTimedOut(ball.ballId, ball.transform.position);
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
