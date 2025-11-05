# PUF the Magic DRAmGON evaluation
This repository contains the source code for the evaluation tools used in our paper `PUF the Magic DRAmGON`.

It is split in two parts:
The `PUF` directory contains the PUF evaluation used in Section 4.
The `DRAmGON` directory contains the evaluation of DRAmGON, our encoding approach described in Section 5.
We called the script `blacksmithSweep/runBlacksmithSweep.sh` from the FlippyRAM ISO image to perform our blacksmith measurement.
In that script, the `END_TIME` variable has to be adjusted to the end of the experiment (otherwise, it will directly stop after being started).
The results from the ISO image were extracted and written to a directory structure following: `<room>/<system_number>/`

## Running PUF evaluation
For PUF evaluation, the `<room>` directories have to be moved into the `PUF/input` directory.

## Running DRAmGON evaluation
`DRAmGON` can be evaluated from the root directory by calling `python DRAmGON/evaluate.py <basedir> <generation-percentage> <keysize>`
For example, using `python evaluation/evaluate.py . 0.2 256` to search for rooms in the current directory, use 20% of the sweeping runs for generation, and encode random 256-bit keys.
