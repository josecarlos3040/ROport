using UnityEngine;

// Coloque este componente no mesmo Cube que possui o BoxCollider da rua.
// O eixo define em que direcao fica a linha central da via.
[RequireComponent(typeof(BoxCollider))]
public class RoadSegment : MonoBehaviour
{
    public enum RoadAxis { HorizontalX, VerticalZ }

    [SerializeField] private RoadAxis axis = RoadAxis.HorizontalX;
    public RoadAxis Axis => axis;
    public BoxCollider Collider => GetComponent<BoxCollider>();
}
