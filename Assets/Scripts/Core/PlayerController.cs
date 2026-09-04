using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("自动小跳")]
    [Tooltip("每次起跳时给予的向上速度")]
    public float jumpVelocity = 7f;
    [Tooltip("两次起跳之间的最小间隔（秒）")]
    public float hopCooldown = 0.35f;

    [Header("左右控制")]
    [Tooltip("空中最大左右速度")]
    public float maxHorizontalSpeed = 8f;

    [Header("落地漏电")]
    [Tooltip("漏电影响半径")]
    public float dischargeRadius = 1.5f;
    [Tooltip("漏电时给自身的额外上推力")]
    public float selfBounceForce = 3f;
    [Tooltip("地面层遮罩，留空则自动使用 Ground")]
    public LayerMask groundLayers;
    public Color dischargeColor = Color.yellow;

    Rigidbody rb;
    float lastJumpTime = -999f;
    bool wasAirborne;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (groundLayers == 0)
            groundLayers = LayerMask.GetMask("Ground");
    }

    void FixedUpdate()
    {
        bool grounded = CheckGrounded();

        // 落地瞬间漏电
        if (grounded && wasAirborne)
        {
            Vector3 point = transform.position + Vector3.down * 0.55f;
            Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 1f, groundLayers);
            if (hit.collider != null) point = hit.point;

            PerformDischarge(point);
            wasAirborne = false;
        }

        // 自动小跳
        if (grounded && Time.time - lastJumpTime > hopCooldown)
        {
            Vector3 vel = rb.velocity;
            vel.y = jumpVelocity;
            rb.velocity = vel;

            lastJumpTime = Time.time;
            wasAirborne = true;
        }

        // 空中左右控制
        float h = Input.GetAxis("Horizontal");
        Vector3 velocity = rb.velocity;
        velocity.x = h * maxHorizontalSpeed;
        rb.velocity = velocity;
    }

    bool CheckGrounded()
    {
        // 从中心向下发射短射线，检测地面
        float rayLength = 0.6f;
        return Physics.Raycast(transform.position, Vector3.down, rayLength, groundLayers);
    }

    void PerformDischarge(Vector3 point)
    {
        // 视觉反馈：落点放电光球
        GameObject fx = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fx.name = "LandingDischargeFX";
        fx.transform.position = point;
        fx.transform.localScale = Vector3.one * dischargeRadius * 0.3f;
        Destroy(fx.GetComponent<Collider>());

        Renderer r = fx.GetComponent<Renderer>();
        if (r != null)
        {
            r.material = new Material(Shader.Find("Standard"));
            r.material.color = dischargeColor;
            r.material.EnableKeyword("_EMISSION");
        }
        Destroy(fx, 0.12f);

        // 给玩家一点反冲上推，增加不可控感
        rb.AddForce(Vector3.up * selfBounceForce, ForceMode.Impulse);

        // 与可放电物体交互
        Collider[] hits = Physics.OverlapSphere(point, dischargeRadius, Physics.AllLayers, QueryTriggerInteraction.Collide);
        foreach (var hit in hits)
        {
            IConductive target = hit.GetComponentInParent<IConductive>();
            if (target != null)
            {
                target.OnDischarge(point, 1f, 0);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, dischargeRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 0.6f);
    }
}
