using System.Collections.Generic;
using DHotMeals.Comps;
using Verse;
using Verse.AI;

namespace DHotMeals;

public static class HeatMealInjector
{
    public static IEnumerable<Toil> InjectHeat(IEnumerable<Toil> values, int num,
        TargetIndex foodIndex = TargetIndex.A, TargetIndex finalLocation = TargetIndex.C)
    {
        using var enumerator = values.GetEnumerator();
        for (var i = 0; i < num; i++)
        {
            enumerator.MoveNext();
            yield return enumerator.Current;
        }

        foreach (var toil in Heat(foodIndex, finalLocation))
        {
            yield return toil;
        }


        while (enumerator.MoveNext())
        {
            yield return enumerator.Current;
        }
    }

    public static IEnumerable<Toil> Heat(TargetIndex foodIndex = TargetIndex.A,
        TargetIndex finalLocation = TargetIndex.C, TargetIndex tableIndex = TargetIndex.None)
    {
        var exit = ToilMaker.MakeToil("ExitPoint");
        var clean = ToilMaker.MakeToil("clean");
        clean.initAction = delegate
        {
            var actor = clean.actor;
            var curJob = actor.jobs.curJob;
            var queue = curJob.GetTargetQueue(TargetIndex.B);
            if (queue.Count > 0)
            {
                queue.RemoveAt(0);
            }
        };
        yield return Toils_Jump.JumpIf(exit, delegate
        {
            var actor = exit.actor;
            var curJob = actor.jobs.curJob;
            LocalTargetInfo food = curJob.GetTarget(foodIndex).Thing;

            var comp = food.Thing?.TryGetComp<CompDFoodTemperature>();
            if (comp == null)
            {
                return true;
            }

            if (comp.PropsTemp.likesHeat)
            {
                return comp.curTemp >= comp.PropsTemp.tempLevels.goodTemp;
            }

            if (HotMealsSettings.thawIt && !comp.PropsTemp.okFrozen)
            {
                return comp.curTemp > 0;
            }

            return true;
        });
        Thing heater = null;
        var getHeater = ToilMaker.MakeToil("GetHeaterToil");
        getHeater.initAction = delegate
        {
            var actor = getHeater.actor;
            var curJob = actor.jobs.curJob;
            var foodToHeat = curJob.GetTarget(foodIndex).Thing;
            Thing table = null;
            if (tableIndex != TargetIndex.None)
            {
                table = curJob.GetTarget(tableIndex).Thing;
            }

            var heater = Toils_HeatMeal.FindPlaceToHeatFood(foodToHeat, actor, searchNear: table);
            if (heater != null)
            {
                curJob.GetTargetQueue(TargetIndex.B).Insert(0, heater);
            }
            else
            {
                curJob.GetTargetQueue(TargetIndex.B).Insert(0, LocalTargetInfo.Invalid);
            }
        };
        yield return getHeater;
        yield return Toils_Jump.JumpIf(clean, () => !curJob.GetTargetQueue(TargetIndex.B)[0].IsValid);
        var targetHeater = curJob.GetTargetQueue(TargetIndex.B)[0];
        if (!HotMealsSettings.multipleHeat)
        {
            yield return Toils_Reserve.Reserve(targetHeater);
            yield return Toils_Goto.GotoThing(targetHeater, PathEndMode.InteractionCell);
            yield return Toils_HeatMeal.HeatMeal(foodIndex, targetHeater).FailOnDespawnedNullOrForbiddenPlacedThings()
                .FailOnCannotTouch(targetHeater, PathEndMode.InteractionCell);
            yield return Toils_Reserve.Release(targetHeater);
        }
        else
        {
            yield return Toils_Goto.GotoThing(targetHeater, PathEndMode.Touch);
            yield return Toils_HeatMeal.HeatMeal(foodIndex, targetHeater).FailOnDespawnedNullOrForbiddenPlacedThings()
                .FailOnCannotTouch(targetHeater, PathEndMode.Touch);
        }

        yield return clean;
        yield return exit;
    }
}