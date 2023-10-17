// 성능 측정 : 코어 개수 8개( 2.2 GHz)
// PipeLine A->B->C->D (4개)
// 작업 1, 2, 3, 4, 5, 6, 7 (7개)
// 측정 결과 : 
// A(동기)->B(동기)->C(동기)->D(동기) : 16200ms
// A(동기)->B(동기)->C(비동기)->D(동기) : 16254ms
// A(동기)->B(비동기)->C(비동기)->D(동기) : 16247ms
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleATI
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            PipelineProcessor workProcessor = new PipelineProcessor();
            Console.ReadLine();

            Stopwatch sw = Stopwatch.StartNew(); //실행시간 체크를 위해 시작
            workProcessor.ProcessWorks().Wait();
            Console.WriteLine($"\n모든 단계 완료!!! : {sw.ElapsedMilliseconds}ms\n"); //실행시간 체크 끝(출력)
        }
    }

    enum TaskType { SingleTask, MultiTask };
    internal class PipelineProcessor
    {
        char[] works = { '1', '2', '3', '4', '5', '6', '7' };
        char[] pipelines = { 'A', 'B', 'C', 'D' };
        private TaskProcessor[] taskProcessors;

        public PipelineProcessor()
        {
            //각 파이프의 태스크 작업 생성
            taskProcessors = new TaskProcessor[]
            {
                new TaskProcessor(pipelines[0], works, TaskType.SingleTask), //동기
                new TaskProcessor(pipelines[1], works, TaskType.SingleTask), //동기
                new TaskProcessor(pipelines[2], works, TaskType.MultiTask), //비동기
                new TaskProcessor(pipelines[3], works, TaskType.SingleTask, lastTask: true) // 동기(마지막 단계 표시)
            };
            //각 파이프의 태스크 파이프라인 연결
            for (int i = 0; i < taskProcessors.Length - 1; i++)
                taskProcessors[i].LinkNextTask(taskProcessors[i + 1]); //A->B->C->D
        }

        public async Task ProcessWorks()
        {
            //파이프라인의 작업 시작
            taskProcessors[0].StartTask();

            //각 파이프 작업이 완료되면 모니터링 결과 출력
            for (int i = 1; i < taskProcessors.Length - 1; i++)
            {
                await taskProcessors[i].WaitForCompletionAsync();
                Console.WriteLine($"[모니터링]:단계 {pipelines[i]} 완료\n");
            }

            //마지막 파이프 작업은 결과를 취합함.
            taskProcessors[taskProcessors.Length - 1].StartTask();
            await taskProcessors[taskProcessors.Length - 1].WaitForCompletionAsync();
            Console.WriteLine($"[최종취합]:단계 {pipelines[taskProcessors.Length - 1]} 완료\n");
        }
    }

    internal class TaskProcessor
    {
        private char pipeID;
        private char[] works;
        private Task[] tasks;
        private TaskType curTaskType;
        private TaskType nextTaskType;
        private AutoResetEvent[] curTaskEvents;//현 태스크의 작업 실행 대기를 위한 이벤트
        private AutoResetEvent[] nextTaskEvents;//다음 태스크에 작업 진행을 알리기 위한 이벤트
        private bool lastTask;

        public TaskProcessor(char pipeID, char[] works, TaskType taskType, bool lastTask = false)
        {
            this.pipeID = pipeID;
            this.works = works;
            this.curTaskType = taskType;
            this.lastTask = lastTask;
            if (taskType == TaskType.SingleTask)
            {
                this.tasks = new Task[1];
                this.curTaskEvents = new AutoResetEvent[] { new AutoResetEvent(false) };
                RunSingleTask();
            }
            else
            {
                this.tasks = new Task[works.Length];
                this.curTaskEvents = works.Select(_ => new AutoResetEvent(false)).ToArray();
                RunMultiTask();
            }
        }
        public void StartTask()
        {
            curTaskEvents[0].Set();
        }
        public async Task WaitForCompletionAsync()
        {
            if (tasks.Length > 0)
                await Task.WhenAll(tasks);
        }
        public void LinkNextTask(TaskProcessor nextTask)
        {
            nextTaskType = nextTask.curTaskType;
            nextTaskEvents = nextTask.lastTask ? null : nextTask.curTaskEvents;
        }
        private void RunSingleTask()
        {
            tasks[0] = Task.Run(() =>
            {
                Console.WriteLine($"{pipeID} Task 생성 작업 대기");
                for (int i = 0; i < works.Length; ++i)
                {
                    curTaskEvents[0].WaitOne(); //현 파이프의 태스크에서 순차 처리를 위해 대기

                    Console.WriteLine($"{pipeID} 작업 {works[i]} 시작.(동기)");
                    Task.Delay(1000).Wait();
                    Console.WriteLine($"{pipeID} 작업 {works[i]} 종료.(동기)");

                    nextTaskEvents?[nextTaskType == TaskType.SingleTask ? 0 : i].Set(); //다음 파이프가 단일 태스크 or 다중 태스크에 따라 작업 진행을 알림
                    curTaskEvents[0].Set(); //현 파이프의 태스크에서 다음 작업을 처리할 수 있도록 알림
                }
            });
        }
        private void RunMultiTask()
        {
            for (int i = 0; i < curTaskEvents.Length; i++)
            {
                int workIndex = i;
                tasks[i] = Task.Run(() =>
                {
                    Console.WriteLine($"{pipeID} Task {works[workIndex]} 생성 작업 대기");
                    curTaskEvents[workIndex].WaitOne(); //현 파이프의 태스크에서 비동기 처리를 위한 대기

                    Console.WriteLine($"{pipeID} 작업 {works[workIndex]} 시작.(비동기)");
                    Task.Delay(1000).Wait();
                    Console.WriteLine($"{pipeID} 작업 {works[workIndex]} 종료.(비동기)");

                    nextTaskEvents?[workIndex].Set(); //마지막 파이프가 아니면 다음 파이프는 다중 태스크이며 작업 진행을 알림
                });
            }
        }
    }
}