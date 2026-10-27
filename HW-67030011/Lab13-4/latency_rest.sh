#!/bin/zsh
# วัดเวลาตอบกลับของเส้นทาง REST: POST /api/led/{on|off} (รวมเขียน Serial + กระจายสถานะ) n รอบ
# usage: ./latency_rest.sh [rounds]
n=${1:-20}
for i in $(seq 1 $n); do
  state=$([ $((i % 2)) -eq 1 ] && echo on || echo off)
  curl -s -o /dev/null -w "%{time_total}\n" -X POST http://localhost:5080/api/led/$state
  sleep 0.15
done | sort -n | awk '{ v[NR] = $1 * 1000; s += v[NR] }
  END { m = (NR % 2) ? v[(NR + 1) / 2] : (v[NR / 2] + v[NR / 2 + 1]) / 2;
        printf "REST POST /api/led    n=%d  min=%.2f  median=%.2f  mean=%.2f  max=%.2f ms\n", NR, v[1], m, s / NR, v[NR] }'
