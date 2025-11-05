#!/bin/bash

END_TIME="2025-10-19 19:00:00"
LOG_FILE="blacksmithRun.log"

if [ "$#" -ne 3 ]; then
	echo "Usage: $0 </path/to/blacksmith/fuzzer> <no-of-ranks> <script/base/path>"
	echo "Note: cd in the directory with the results before running."
	exit 255
fi

BLACKSMITH_PATH="$1"
No_Ranks="$2"
SCRIPT_BASE_PATH="$3"

getNextNumber() {
	lastNumber="$(printf %d "$(ls | grep "$1" | sort -h | tail -n1| cut -d "_" -f 2 | sed 's/^[0]*//g')")"
	if [ "$lastNumber" == "" ]; then
		lastNumber="0"
	fi

	nextNumber="$(printf %03d $(($lastNumber + 1)))"
	echo $1$nextNumber
}

runFuzzing() {
	fuzzingDir=$(getNextNumber "fuzzing_")
	echo "[$(date +"%Y-%m-%d %H:%M:%S")]: Starting run $fuzzingDir" >> $LOG_FILE
	echo "[$(date +"%Y-%m-%d %H:%M:%S")]: Starting run $fuzzingDir"
	echo "[DEBUG]: $BLACKSMITH_PATH --dimm-id=1 --ranks $No_Ranks --runtime-limit=3600 --sweeping > /dev/null 2>&1"
	$BLACKSMITH_PATH --dimm-id=1 --ranks $No_Ranks --runtime-limit=3600 --sweeping > /dev/null 2>&1
	mkdir $fuzzingDir
	mv stdout.log $fuzzingDir/
	mv fuzz-summary.json $fuzzingDir/
	echo "[$(date +"%Y-%m-%d %H:%M:%S")]: Finished run $fuzzingDir" >> $LOG_FILE
	echo "[$(date +"%Y-%m-%d %H:%M:%S")]: Finished run $fuzzingDir"

	bash $SCRIPT_BASE_PATH/adjustFuzzSummary.sh $fuzzingDir/stdout.log $SCRIPT_BASE_PATH
}

runSweeping() {
	sweepingDir=$(getNextNumber "sweeping_")
	echo "[$(date +"%Y-%m-%d %H:%M:%S")]: Starting run $sweepingDir" >> $LOG_FILE
	echo "[$(date +"%Y-%m-%d %H:%M:%S")]: Starting run $sweepingDir"
	echo "[DEBUG]: $BLACKSMITH_PATH --dimm-id=1 --ranks $No_Ranks --load-json $1 --sweeping > /dev/null 2>&1"
	$BLACKSMITH_PATH --dimm-id=1 --ranks $No_Ranks --load-json $1 --sweeping > /dev/null 2>&1
	mkdir $sweepingDir
	mv stdout.log $sweepingDir/
	mv sweep-summary-1x256MB.json $sweepingDir/
	echo "[$(date +"%Y-%m-%d %H:%M:%S")]: Finished run $sweepingDir" >> $LOG_FILE
	echo "[$(date +"%Y-%m-%d %H:%M:%S")]: Finished run $sweepingDir"
}

end_epoch="$(date -d "$END_TIME" +"%s")"

while [ "$(date +"%s")" -lt "$end_epoch" ]; do
	files="$(find -name final-fuzz-summary.json)"
	nFiles="$(find -name final-fuzz-summary.json | wc -l)"

	if [ "$nFiles" == "0" ]; then
		runFuzzing
	elif [ "$nFiles" == "1" ]; then
		runSweeping "$files"
	else
		echo "[$(date +"%Y-%m-%d %H:%M:%S")]: Error: There is more than one final-fuzz-summary.json file present in $(pwd). Exiting." >> $LOG_FILE
		echo "[$(date +"%Y-%m-%d %H:%M:%S")]: Error: There is more than one final-fuzz-summary.json file present in $(pwd). Exiting."
		exit 255
	fi
done
