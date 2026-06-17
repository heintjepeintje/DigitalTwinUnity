using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class BusAgent : Agent
{
    
    [Header("Rijinstellingen")]
    public float rijSnelheid = 15f;       
    public float stuurKracht = 100f;      
    public float maxSnelheid = 20f;       

    [Header("Checkpoints en Route")]
    public Transform[] checkpoints;       
    public Transform eindbestemming;      

    [Header("Verwijzingen")]
    public Rigidbody rb;                  
    
    private int huidigCheckpointIndex = 0;

    private Vector3 startPositie;
    private Quaternion startRotatie;


    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        startPositie = transform.localPosition;
        startRotatie = transform.localRotation;
    }

   
    public override void OnEpisodeBegin()
    {
        // Reset de bus naar startpositie
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.localPosition = startPositie;
        transform.localRotation = startRotatie;

        // Reset voortgang
        huidigCheckpointIndex = 0;

    }


    public override void CollectObservations(VectorSensor sensor)
    {
        // Snelheid van de bus (3 waarden: x, y, z)
        sensor.AddObservation(rb.linearVelocity / maxSnelheid);

        // Richting naar het volgende checkpoint (3 waarden)
        if (huidigCheckpointIndex < checkpoints.Length)
        {
            Vector3 richting = (checkpoints[huidigCheckpointIndex].position
                                - transform.position).normalized;
            sensor.AddObservation(richting);

            // Afstand naar het volgende checkpoint (1 waarde)
            float afstand = Vector3.Distance(transform.position,
                                             checkpoints[huidigCheckpointIndex].position);
            sensor.AddObservation(afstand / 100f);
        }
        else
        {
            sensor.AddObservation(Vector3.zero); // 3 waarden
            sensor.AddObservation(0f);           // 1 waarde
        }

        // Welke kant de bus op kijkt (3 waarden)
        sensor.AddObservation(transform.forward);
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        float gas = actions.ContinuousActions[0];
        float stuur = actions.ContinuousActions[1];

        // Rijden
        Vector3 rijKracht = transform.forward * gas * rijSnelheid;
        rb.AddForce(rijKracht, ForceMode.VelocityChange);

        // Snelheidslimiet
        if (rb.linearVelocity.magnitude > maxSnelheid)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSnelheid;

        // Sturen
        float rotatie = stuur * stuurKracht * Time.fixedDeltaTime;
        transform.Rotate(0f, rotatie, 0f);



        // Kleine straf elke frame AI leert snel te zijn
        AddReward(-0.001f);

        // Beloning als de bus de goede richting op rijdt
        if (huidigCheckpointIndex < checkpoints.Length)
        {
            Vector3 naarCheckpoint = (checkpoints[huidigCheckpointIndex].position
                                      - transform.position).normalized;
            float uitlijning = Vector3.Dot(transform.forward, naarCheckpoint);
            AddReward(uitlijning * 0.002f);
        }
    }

 


    private void OnTriggerEnter(Collider other)
    {
        // Juist checkpoint geraakt
        if (other.CompareTag("Checkpoint"))
        {
            int index = System.Array.IndexOf(checkpoints, other.transform);
            if (index == huidigCheckpointIndex)
            {
                AddReward(1.0f);
                huidigCheckpointIndex++;
                Debug.Log($"Checkpoint {index} bereikt! Totale reward: {GetCumulativeReward()}");
            }
            else
            {
                AddReward(-0.3f); // verkeerd checkpoint
            }
        }

        // Eindbestemming bereikt
        if (other.CompareTag("Eindbestemming"))
        {
            AddReward(5.0f);
            Debug.Log("Route voltooid!");
            EndEpisode();
        }

        // Botsing met muur of obstakel
        if (other.CompareTag("Muur") || other.CompareTag("Obstakel"))
        {
            AddReward(-1.0f);
            Debug.Log("Botsing! Episode opnieuw.");
            EndEpisode();
        }
    }


    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;
        continuous[0] = Input.GetAxis("Vertical");   // W/S = gas/rem
        continuous[1] = Input.GetAxis("Horizontal"); // A/D = sturen
    }
}