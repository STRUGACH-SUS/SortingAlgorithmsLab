using SortingLab.Core.Abstractions;
using SortingLab.Core.Execution;
using SortingLab.Core.Models;

namespace SortingLab.Core.Algorithms.Internal;

/// <summary>
/// Гномья сортировка: текущий элемент сравнивается с предыдущим.
/// При нарушении порядка элементы меняются местами, после чего алгоритм
/// возвращается на одну позицию назад.
/// </summary>
public sealed class GnomeSort : IArraySortAlgorithm
{
    public AlgorithmDescriptor Descriptor { get; } = new(
        Id: "gnome",
        Name: "Gnome Sort",
        Category: "Квадратичный алгоритм",
        ShortDescription: "Гномья сортировка: алгоритм идёт вперёд, а при нарушении порядка меняет соседние элементы и возвращается назад.",
        Idea: "Гномья сортировка (Gnome Sort) напоминает движение гнома по массиву: пока порядок сохраняется, он идёт вперёд; при нарушении порядка меняет местами текущий и предыдущий элементы и делает шаг назад.",
        Principle: "Начинаем с позиции 1. Если текущий элемент не меньше предыдущего, переходим вперёд. Иначе меняем элементы местами и возвращаемся на одну позицию назад. Процесс продолжается до конца массива.",
        TimeComplexity: "Лучшее O(n); среднее и худшее O(n²)",
        SpaceComplexity: "O(1) дополнительной памяти",
        Features: "Устойчивый алгоритм. На почти отсортированных данных работает заметно быстрее; не требует дополнительного массива.");

    public async Task<ArraySortResult> SortAsync(
        IReadOnlyList<int> values,
        IStepSink<ArraySortStep> stepSink,
        CancellationToken cancellationToken = default)
    {
        var array = values.ToArray();
        var context = new ArraySortContext(array, stepSink);
        await context.InitialAsync(cancellationToken);

        if (array.Length <= 1)
        {
            await context.CompleteAsync(
                array,
                array.Length == 0
                    ? "Массив пуст — сортировать нечего."
                    : "В массиве один элемент, поэтому он уже отсортирован.",
                cancellationToken);
            return new ArraySortResult((int[])array.Clone(), context.Statistics);
        }

        var position = 1;
        await context.PositionAsync(
            position,
            "Начинаем с позиции 1. Сравниваем текущий элемент с предыдущим.",
            cancellationToken);

        while (position < array.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (position == 0)
            {
                position = 1;
                await context.PositionAsync(
                    position,
                    "Вернулись к началу массива. Продолжаем движение вперёд.",
                    cancellationToken);
            }

            var previous = array[position - 1];
            var current = array[position];
            var isOrdered = previous <= current;

            await context.CompareAsync(
                position - 1,
                position,
                isOrdered
                    ? $"Сравниваем {previous} и {current}. Числа расположены правильно, поэтому переходим дальше."
                    : $"Сравниваем {previous} и {current}. Текущее число меньше предыдущего, поэтому нужна перестановка.",
                cancellationToken);

            if (isOrdered)
            {
                position++;
                await context.PositionAsync(
                    position,
                    position < array.Length
                        ? $"Порядок не нарушен. Переходим к позиции {position}."
                        : "Достигли конца массива — сортировка завершена.",
                    cancellationToken);
            }
            else
            {
                await context.SwapAsync(
                    position - 1,
                    position,
                    $"Меняем местами {previous} и {current}: меньшее число должно стоять левее.",
                    cancellationToken);

                position--;
                await context.PositionAsync(
                    position,
                    position == 0
                        ? "Возвращаемся на одну позицию назад. Дошли до начала массива."
                        : "Возвращаемся на одну позицию назад, чтобы проверить порядок.",
                    cancellationToken);
            }
        }

        await context.CompleteAsync(
            array,
            "Гном дошёл до конца массива. Все элементы расположены по возрастанию.",
            cancellationToken);

        return new ArraySortResult((int[])array.Clone(), context.Statistics);
    }
}
