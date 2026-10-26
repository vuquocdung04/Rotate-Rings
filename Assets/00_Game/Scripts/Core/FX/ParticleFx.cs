using System.Collections.Generic;
using UnityEngine;

public class ParticleFx : MonoBehaviour
{
    [SerializeField] private List<ParticleSystem> listParticle = new List<ParticleSystem>();
    [SerializeField] private float despawnTimeout = 3f;

    private int playId;

    public IReadOnlyList<ParticleSystem> ListParticle => listParticle;

    public bool IsPlaying
    {
        get
        {
            for (int i = 0; i < listParticle.Count; i++)
            {
                if (listParticle[i] != null && listParticle[i].IsAlive(true)) return true;
            }

            return false;
        }
    }

    public void Play()
    {
        for (int i = 0; i < listParticle.Count; i++)
        {
            if (listParticle[i] == null) continue;

            listParticle[i].Clear(true);
            listParticle[i].Play(true);
        }
    }

    public void Play(Vector3 position)
    {
        transform.position = position;
        Play();
    }

    public void Play(Transform target)
    {
        if (target != null) transform.SetPositionAndRotation(target.position, target.rotation);
        Play();
    }

    public void PlayPooled()
    {
        Play();
        DespawnWhenDoneAsync(++playId).Forget();
    }

    public void Stop()
    {
        for (int i = 0; i < listParticle.Count; i++)
        {
            if (listParticle[i] != null) listParticle[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private async Awaitable DespawnWhenDoneAsync(int id)
    {
        var token = destroyCancellationToken;
        float elapsed = 0f;

        await Awaitable.NextFrameAsync(token);

        while (IsPlaying && elapsed < despawnTimeout)
        {
            if (id != playId) return;

            elapsed += Time.deltaTime;
            await Awaitable.NextFrameAsync(token);
        }

        if (id == playId) SimplePool2.Despawn(gameObject);
    }
}
