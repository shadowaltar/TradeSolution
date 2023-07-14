package com.sandbox.services;

import org.springframework.stereotype.Service;

import java.io.File;
import java.util.Arrays;

@Service
public class FancyFileService implements FileService {
    @Override
    public File[] getRootFolderFiles(String diskLabel) {
            var windowsRoots = listRoots();
            var drive = Arrays.stream(windowsRoots)
                    .filter(i -> i.getPath().startsWith(diskLabel))
                    .findFirst();
            if (drive.isEmpty())
                return new File[0];
            return listFiles(drive.get());
    }

    @Override
    public File[] listRoots() {
        return File.listRoots();
    }

    @Override
    public File[] listFiles(File file) {
        return file.listFiles();
    }
}
