using System.Collections.Generic;
using UnityEngine;

public class Animal : LivingEntity
{
    Environment environment;
    public const int maxViewDistance = 10;

    [EnumFlags]
    public Species diet;

    public CreatureAction currentAction;
    public Genes genes;
    public Color maleColour;
    public Color femaleColour;

    // Settings:
    float timeBetweenActionChoices = 1;
    float moveSpeed = 1.5f;
    float timeToDeathByHunger = 200;
    float timeToDeathByThirst = 200;
    float timeToReproduce = 200;

    float drinkDuration = 6;
    float eatDuration = 10;

    float criticalPercent = 0.7f;

    // Visual settings:
    float moveArcHeight = .2f;

    // State:
    [Header("State")]
    public float hunger;
    public float thirst;
    public float reproUrge; //reproduction urge

    protected LivingEntity foodTarget;
    protected Coord waterTarget;
    protected Animal mateTarget;

    // Move data:
    bool animatingMovement;
    Coord moveFromCoord;
    Coord moveTargetCoord;
    Vector3 moveStartPos;
    Vector3 moveTargetPos;
    float moveTime;
    float moveSpeedFactor;
    float moveArcHeightFactor;
    Coord[] path;
    int pathIndex;

    // Other
    float lastActionChooseTime;
    const float sqrtTwo = 1.4142f;
    const float oneOverSqrtTwo = 1 / sqrtTwo;

    public override void Init(Coord coord)
    {
        environment = FindObjectOfType<Environment>();
        base.Init(coord);
        moveFromCoord = coord;
        genes = Genes.RandomGenes(1);

        material.color = (genes.isMale) ? maleColour : femaleColour;

        ChooseNextAction();
    }



    protected virtual void Update()
    {

        // Increase hunger and thirst over time
        hunger += Time.deltaTime * 1 / timeToDeathByHunger;
        thirst += Time.deltaTime * 1 / timeToDeathByThirst;
        reproUrge += Time.deltaTime * 1 / timeToReproduce;

        // Animate movement. After moving a single tile, the animal will be able to choose its next action
        if (animatingMovement)
        {
            AnimateMove();
        }
        else
        {
            // Handle interactions with external things, like food, water, mates
            HandleInteractions();
            float timeSinceLastActionChoice = Time.time - lastActionChooseTime;
            if (timeSinceLastActionChoice > timeBetweenActionChoices)
            {
                ChooseNextAction();
            }
        }

        if (hunger >= 1)
        {
            Die(CauseOfDeath.Hunger);
        }
        else if (thirst >= 1)
        {
            Die(CauseOfDeath.Thirst);
        }
    }

    // Animals choose their next action after each movement step (1 tile),
    // or, when not moving (e.g interacting with food etc), at a fixed time interval
    protected virtual void ChooseNextAction()
    {
        lastActionChooseTime = Time.time;
        // Get info about surroundings

        // Decide next action:
        // Eat if (more hungry than thirsty) or (currently eating and not critically thirsty)
        bool currentlyEating = currentAction == CreatureAction.Eating && foodTarget && hunger > 0;
        bool urgeToMate = reproUrge > .1f && hunger < criticalPercent;
        if (urgeToMate)
        {
            FindMate();
            Debug.Log(this.gameObject.name + " looking for mate");
        }
        else if (hunger >= thirst || currentlyEating && thirst < criticalPercent)
        {
            FindFood();
        }
        // More thirsty than hungry and urge to mate
        else
        {
            FindWater();
        }

        Act();

    }

    protected virtual void FindFood()
    {
        LivingEntity foodSource = Environment.SenseFood(coord, this, FoodPreferencePenalty);
        if (foodSource)
        {
            currentAction = CreatureAction.GoingToFood;
            foodTarget = foodSource;
            CreatePath(foodTarget.coord);
        }
        else
        {
            currentAction = CreatureAction.Exploring;
        }
    }
    protected virtual void FindMate()
    {
        List<Animal> mate = Environment.SensePotentialMates(coord, this);
        currentAction = CreatureAction.SearchingForMate;

        if (mate.Count > 0 && this.genes.isMale)
        {
            mateTarget = mate[mate.Count - 1];
            CreatePath(mateTarget.coord);
        }
        else if(mate.Count > 0 && !this.genes.isMale)
        {
            mateTarget = mate[mate.Count - 1];
        }
        else if(!this.genes.isMale)
        {
            currentAction = CreatureAction.SearchingForMate;
        }
        else if(this.genes.isMale)
        {
            currentAction= CreatureAction.Exploring;
        }
    }
    protected virtual void FindWater()
    {
        Coord waterTile = Environment.SenseWater(coord);
        if (waterTile != Coord.invalid)
        {
            currentAction = CreatureAction.GoingToWater;
            waterTarget = waterTile;
            CreatePath(waterTarget);
        }
        else
        {
            currentAction = CreatureAction.Exploring;
        }
    }

    // When choosing from multiple food sources, the one with the lowest penalty will be selected
    protected virtual int FoodPreferencePenalty(LivingEntity self, LivingEntity food)
    {
        return Coord.SqrDistance(self.coord, food.coord);
    }

    protected void Act()
    {
        switch (currentAction)
        {
            case CreatureAction.Exploring:
                StartMoveToCoord(Environment.GetNextTileWeighted(coord, moveFromCoord));
                break;
            case CreatureAction.GoingToFood:
                if (Coord.AreNeighbours(coord, foodTarget.coord))
                {
                    LookAt(foodTarget.coord);
                    currentAction = CreatureAction.Eating;
                }
                else
                {
                    StartMoveToCoord(path[pathIndex]);
                    pathIndex++;
                }
                break;
            case CreatureAction.GoingToWater:
                if (Coord.AreNeighbours(coord, waterTarget))
                {
                    LookAt(waterTarget);
                    currentAction = CreatureAction.Drinking;
                }
                else
                {
                    StartMoveToCoord(path[pathIndex]);
                    pathIndex++;
                }
                break;
            case CreatureAction.SearchingForMate:
                if (mateTarget != null && Coord.AreNeighbours(coord, mateTarget.coord))
                {
                    LookAt(mateTarget.coord);
                    currentAction = CreatureAction.Fornicating;
                }
                else if(mateTarget != null && genes.isMale)
                {
                    StartMoveToCoord(path[pathIndex]);
                    pathIndex++;
                }
                else 
                {
                    StartMoveToCoord(Environment.GetNextTileWeighted(coord, moveFromCoord));
                }
                break;
        }
    }

    protected void CreatePath(Coord target)
    {
        // Create new path if current is not already going to target
        if (path == null || pathIndex >= path.Length || (path[path.Length - 1] != target || path[pathIndex - 1] != moveTargetCoord))
        {
            path = EnvironmentUtility.GetPath(coord.x, coord.y, target.x, target.y);
            pathIndex = 0;
        }
    }

    protected void StartMoveToCoord(Coord target)
    {
        moveFromCoord = coord;
        moveTargetCoord = target;
        moveStartPos = transform.position;
        moveTargetPos = Environment.tileCentres[moveTargetCoord.x, moveTargetCoord.y];
        animatingMovement = true;

        bool diagonalMove = Coord.SqrDistance(moveFromCoord, moveTargetCoord) > 1;
        moveArcHeightFactor = (diagonalMove) ? sqrtTwo : 1;
        moveSpeedFactor = (diagonalMove) ? oneOverSqrtTwo : 1;

        LookAt(moveTargetCoord);
    }

    protected void LookAt(Coord target)
    {
        if (target != coord)
        {
            Coord offset = target - coord;
            transform.eulerAngles = Vector3.up * Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;
        }
    }

    void HandleInteractions()
    {
        if (this.species == Species.Rabbit)
        {
            if (currentAction == CreatureAction.Eating)
            {
                if (foodTarget && hunger > 0)
                {
                    float eatAmount = Mathf.Min(hunger, Time.deltaTime * 1 / eatDuration);
                    eatAmount = ((Plant)foodTarget).Consume(eatAmount);
                    hunger -= eatAmount;
                }
            }
            else if (currentAction == CreatureAction.Drinking)
            {
                if (thirst > 0)
                {
                    thirst -= Time.deltaTime * 1 / drinkDuration;
                    thirst = Mathf.Clamp01(thirst);
                }
            }
            else if (currentAction == CreatureAction.Fornicating)
            {
                Mate();
            }
        }

        else if (this.species == Species.Fox)
        {
            if (currentAction == CreatureAction.Eating)
            {
                if (foodTarget && hunger > 0)
                {
                    foodTarget.Die(CauseOfDeath.Eaten);
                    hunger -= .01f;
                }
            }
            else if (currentAction == CreatureAction.Drinking)
            {
                if (thirst > 0)
                {
                    thirst -= Time.deltaTime * 1 / drinkDuration;
                    thirst = Mathf.Clamp01(thirst);
                }
            }
            else if (currentAction == CreatureAction.Fornicating)
            {
                Mate();
            }
        }
    }

    private void Mate()
    {
        if (this.species == Species.Fox)
        {
            if (!this.genes.isMale)
            {
                LivingEntity fox = Instantiate(environment.initialPopulations[2].prefab);
                fox.coord = this.coord;
                fox.Init(fox.coord);
                Debug.Log("Fox birth succesfull");
                Environment.speciesMaps[fox.species].Add(fox, fox.coord);
            }
            reproUrge = 0f;
            currentAction = CreatureAction.Exploring;
        }

        if (this.species == Species.Rabbit)
        {
            if (!this.genes.isMale)
            {
                LivingEntity rabbit = Instantiate(environment.initialPopulations[0].prefab);
                rabbit.coord = this.coord;
                rabbit.Init(rabbit.coord);
                Debug.Log("Rabbit birth succesfull");
                Environment.speciesMaps[rabbit.species].Add(rabbit, rabbit.coord);
            }
            reproUrge = 0f;
            currentAction = CreatureAction.Exploring;
        }
    }

    void AnimateMove()
    {
        // Move in an arc from start to end tile
        moveTime = Mathf.Min(1, moveTime + Time.deltaTime * moveSpeed * moveSpeedFactor);
        float height = (1 - 4 * (moveTime - .5f) * (moveTime - .5f)) * moveArcHeight * moveArcHeightFactor;
        transform.position = Vector3.Lerp(moveStartPos, moveTargetPos, moveTime) + Vector3.up * height;

        // Finished moving
        if (moveTime >= 1)
        {
            Environment.RegisterMove(this, coord, moveTargetCoord);
            coord = moveTargetCoord;

            animatingMovement = false;
            moveTime = 0;
            ChooseNextAction();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            var surroundings = Environment.Sense(coord);
            Gizmos.color = Color.white;
            if (surroundings.nearestFoodSource != null)
            {
                Gizmos.DrawLine(transform.position, surroundings.nearestFoodSource.transform.position);
            }
            if (surroundings.nearestWaterTile != Coord.invalid)
            {
                Gizmos.DrawLine(transform.position, Environment.tileCentres[surroundings.nearestWaterTile.x, surroundings.nearestWaterTile.y]);
            }

            if (currentAction == CreatureAction.GoingToFood)
            {
                var path = EnvironmentUtility.GetPath(coord.x, coord.y, foodTarget.coord.x, foodTarget.coord.y);
                Gizmos.color = Color.black;
                if (foodTarget != null)
                {
                    for (int i = 0; i < path.Length; i++)
                    {
                        Gizmos.DrawSphere(Environment.tileCentres[path[i].x, path[i].y], .2f);
                    }
                }
            }
        }
    }
}

