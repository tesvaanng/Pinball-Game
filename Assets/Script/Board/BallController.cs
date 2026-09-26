using UnityEngine;
using Pinball.Core;

namespace Pinball.Board
{
    public class BallController : MonoBehaviour
    {
        public int ballId;
        public BigNumber score = BigNumber.Zero;
        public float lifeTime;
        public float maxLifeTime = 8f;
        public bool reported;

        private void OnEnable()
        {
            lifeTime = 0f;
            reported = false;
            score = BigNumber.Zero;
        }

        private void Update()
        {
            if (reported) return;

            lifeTime += Time.deltaTime;
            if (lifeTime >= maxLifeTime)
            {
                reported = true;
                if (BoardManager.IsInitialized)
                {
                    BoardManager.Instance.ReportTimeout(this);
                }
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (reported) return;

            Peg peg = collision.gameObject.GetComponent<Peg>();
            if (peg != null && BoardManager.IsInitialized)
            {
                BigNumber gained = peg.score;
                score += gained;
                BoardManager.Instance.ReportScoreChanged(this, gained);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (reported) return;

            Pocket pocket = other.GetComponent<Pocket>();
            if (pocket != null && BoardManager.IsInitialized)
            {
                reported = true;
                BoardManager.Instance.ReportPocket(this, pocket.multiplier);
            }
        }
    }
}
